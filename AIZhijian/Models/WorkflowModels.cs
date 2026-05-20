using System.Text.Json.Serialization;

namespace AIZhijian.Models;

public abstract class WorkflowNodeConfig { }

public class TextInputNodeConfig : WorkflowNodeConfig
{
    public string Text { get; set; } = "";
    public string Type => "textInput";
}

public class PromptTemplateNodeConfig : WorkflowNodeConfig
{
    public string Template { get; set; } = "";
    public string Type => "promptTemplate";
}

public class ImageGenNodeConfig : WorkflowNodeConfig
{
    public string GenType { get; set; } = "gpt-image";
    public string Channel { get; set; } = "official";
    public string AspectRatio { get; set; } = "9:16";
    public string Resolution { get; set; } = "2k";
    public string Quality { get; set; } = "medium";
    public bool PhotoReal { get; set; }
    public string Type => "imageGen";
}

public class VideoGenNodeConfig : WorkflowNodeConfig
{
    public string GenType { get; set; } = "veo";
    public string Channel { get; set; } = "budget";
    public string Model { get; set; } = "fast";
    public string Mode { get; set; } = "text";
    public string AspectRatio { get; set; } = "9:16";
    public string Resolution { get; set; } = "720p";
    public string Duration { get; set; } = "8";
    public bool GenerateAudio { get; set; }
    public string NegativePrompt { get; set; } = "";
    public int Count { get; set; } = 1;
    public string Type => "videoGen";
}

public class ResultOutputNodeConfig : WorkflowNodeConfig
{
    public string Label { get; set; } = "最终结果";
    public string Type => "resultOutput";
}

public class WorkflowPort
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string PortType { get; set; } = "any";
    public string NodeId { get; set; } = "";
    public string Role { get; set; } = "text";
}

public class WorkflowNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public string NodeType { get; set; } = "";
    public object? Config { get; set; }
    public List<WorkflowPort> InputPorts { get; set; } = new();
    public List<WorkflowPort> OutputPorts { get; set; } = new();
}

public class WorkflowEdge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = "";
    public string SourcePortId { get; set; } = "";
    public string TargetNodeId { get; set; } = "";
    public string TargetPortId { get; set; } = "";
}

public class WorkflowDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public List<WorkflowNode> Nodes { get; set; } = new();
    public List<WorkflowEdge> Edges { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public List<string> Validate()
    {
        var errors = new List<string>();

        if (Nodes.Count == 0)
        {
            errors.Add("工作流至少需要一个节点");
            return errors;
        }

        var nodeIds = new HashSet<string>();
        foreach (var node in Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
                errors.Add("节点 ID 不能为空");
            else if (!nodeIds.Add(node.Id))
                errors.Add($"节点 ID \"{node.Id}\" 重复");

            if (string.IsNullOrWhiteSpace(node.NodeType))
                errors.Add($"节点 \"{node.Id}\" 缺少类型 (NodeType)");

            if (node.Config == null)
                errors.Add($"节点 \"{node.Id}\" 缺少配置 (Config)");
        }

        foreach (var edge in Edges)
        {
            if (string.IsNullOrWhiteSpace(edge.SourceNodeId))
                errors.Add($"边 \"{edge.Id}\" 的源节点为空");
            else if (!nodeIds.Contains(edge.SourceNodeId))
                errors.Add($"边 \"{edge.Id}\" 引用了不存在的源节点 \"{edge.SourceNodeId}\"");

            if (string.IsNullOrWhiteSpace(edge.TargetNodeId))
                errors.Add($"边 \"{edge.Id}\" 的目标节点为空");
            else if (!nodeIds.Contains(edge.TargetNodeId))
                errors.Add($"边 \"{edge.Id}\" 引用了不存在的目标节点 \"{edge.TargetNodeId}\"");

            if (!string.IsNullOrWhiteSpace(edge.SourceNodeId)
                && edge.SourceNodeId == edge.TargetNodeId
                && !string.IsNullOrWhiteSpace(edge.TargetNodeId))
                errors.Add($"边 \"{edge.Id}\" 是自环 (源节点等于目标节点)");
        }

        return errors;
    }
}
