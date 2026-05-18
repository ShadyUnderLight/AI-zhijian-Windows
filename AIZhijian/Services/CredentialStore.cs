using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace AIZhijian.Services;

public class SavedCredentials
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public static class CredentialStore
{
    private const string TargetName = "AIZhijian_SavedLogin";

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredReadW(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWriteW([In] ref CREDENTIALW credential, int flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDeleteW(string target, int type, int flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void ZeroMemory(IntPtr ptr, int size);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIALW
    {
        public int Flags;
        public int Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    private const int CRED_TYPE_GENERIC = 1;

    public static SavedCredentials? Load()
    {
        if (!CredReadW(TargetName, CRED_TYPE_GENERIC, 0, out var credPtr))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIALW>(credPtr);
            string userName;
            if (cred.UserName != IntPtr.Zero)
                userName = Marshal.PtrToStringUni(cred.UserName) ?? "";
            else
                return null;

            byte[] blob = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, blob, 0, blob.Length);
            var json = Encoding.UTF8.GetString(blob);

            var sc = JsonSerializer.Deserialize<SavedCredentials>(json);
            if (sc != null) sc.Username = userName;
            return sc;
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    public static void Save(string username, string password)
    {
        Delete();

        var json = JsonSerializer.Serialize(new SavedCredentials { Username = username, Password = password });
        var blob = Encoding.UTF8.GetBytes(json);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        Marshal.Copy(blob, 0, blobPtr, blob.Length);

        var targetPtr = Marshal.StringToHGlobalUni(TargetName);
        var userPtr = Marshal.StringToHGlobalUni(username);

        try
        {
            var cred = new CREDENTIALW
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = targetPtr,
                UserName = userPtr,
                CredentialBlob = blobPtr,
                CredentialBlobSize = blob.Length,
                Persist = 2
            };
            CredWriteW(ref cred, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
            Marshal.FreeHGlobal(targetPtr);
            Marshal.FreeHGlobal(userPtr);
        }
    }

    public static void Delete()
    {
        CredDeleteW(TargetName, CRED_TYPE_GENERIC, 0);
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr cred);
}
