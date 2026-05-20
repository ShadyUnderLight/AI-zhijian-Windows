using AIZhijian.Models;

namespace AIZhijian.Tests;

public class WorkflowModelValidationTests
{
    [Fact]
    public void Empty_graph_returns_error()
    {
        var wf = new WorkflowDefinition();
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("至少需要一个节点"));
    }

    [Fact]
    public void Single_valid_node_passes()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig() } }
        };
        var errors = wf.Validate();
        Assert.Empty(errors);
    }

    [Fact]
    public void Duplicate_node_id_returns_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes =
            {
                new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig() },
                new WorkflowNode { Id = "n1", NodeType = "imageGen", Config = new ImageGenNodeConfig() }
            }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("重复"));
    }

    [Fact]
    public void Missing_node_type_returns_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", Config = new TextInputNodeConfig() } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("缺少类型"));
    }

    [Fact]
    public void Whitespace_node_type_returns_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "   ", Config = new TextInputNodeConfig() } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("缺少类型"));
    }

    [Fact]
    public void Missing_node_config_returns_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "textInput" } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("缺少配置"));
    }

    [Fact]
    public void Empty_node_id_returns_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "", NodeType = "textInput", Config = new TextInputNodeConfig() } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("不能为空"));
    }

    [Fact]
    public void Edge_with_empty_source_returns_clearer_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig() } },
            Edges = { new WorkflowEdge { Id = "e1", SourceNodeId = "", TargetNodeId = "n1" } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("源节点为空"));
    }

    [Fact]
    public void Edge_with_empty_target_returns_clearer_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig() } },
            Edges = { new WorkflowEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "" } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("目标节点为空"));
    }

    [Fact]
    public void Edge_to_nonexistent_source_returns_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig() } },
            Edges = { new WorkflowEdge { Id = "e1", SourceNodeId = "missing", TargetNodeId = "n1" } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("不存在的源节点"));
    }

    [Fact]
    public void Edge_to_nonexistent_target_returns_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig() } },
            Edges = { new WorkflowEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "missing" } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("不存在的目标节点"));
    }

    [Fact]
    public void Self_loop_edge_returns_error()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig() } },
            Edges = { new WorkflowEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n1" } }
        };
        var errors = wf.Validate();
        Assert.Contains(errors, e => e.Contains("自环"));
    }

    [Fact]
    public void Empty_edge_source_target_does_not_false_positive_self_loop()
    {
        var wf = new WorkflowDefinition
        {
            Nodes = { new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig() } },
            Edges = { new WorkflowEdge { Id = "e1", SourceNodeId = "", TargetNodeId = "" } }
        };
        var errors = wf.Validate();
        Assert.DoesNotContain(errors, e => e.Contains("自环"));
    }

    [Fact]
    public void Multiple_errors_returned_together()
    {
        var wf = new WorkflowDefinition
        {
            Nodes =
            {
                new WorkflowNode { Id = "", NodeType = "", Config = null },
                new WorkflowNode { Id = "", NodeType = "", Config = null }
            },
            Edges = { new WorkflowEdge { Id = "e1", SourceNodeId = "ghost", TargetNodeId = "phantom" } }
        };
        var errors = wf.Validate();
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Complete_workflow_passess_validation()
    {
        var wf = new WorkflowDefinition
        {
            Nodes =
            {
                new WorkflowNode { Id = "n1", NodeType = "textInput", Config = new TextInputNodeConfig { Text = "hello" } },
                new WorkflowNode { Id = "n2", NodeType = "imageGen", Config = new ImageGenNodeConfig() },
                new WorkflowNode { Id = "n3", NodeType = "resultOutput", Config = new ResultOutputNodeConfig() }
            },
            Edges =
            {
                new WorkflowEdge { Id = "e1", SourceNodeId = "n1", TargetNodeId = "n2" },
                new WorkflowEdge { Id = "e2", SourceNodeId = "n2", TargetNodeId = "n3" }
            }
        };
        var errors = wf.Validate();
        Assert.Empty(errors);
    }
}
