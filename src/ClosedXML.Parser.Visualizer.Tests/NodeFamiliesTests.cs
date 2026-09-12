using ClosedXML.Parser.Visualizer.Diagram;

namespace ClosedXML.Parser.Visualizer.Tests;

public class NodeFamiliesTests
{
    [Fact]
    public void Each_node_type_has_a_family()
    {
        var nodeTypes = typeof(AstNode).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(AstNode)) && !type.IsAbstract && type != typeof(ValueNode))
            .Select(type => type.Name[..^"Node".Length])
            .Concat(["Blank", "Logical", "Error", "Number", "Text"])
            .ToList();

        Assert.NotEmpty(nodeTypes);
        Assert.All(nodeTypes, type => Assert.Contains(type, NodeFamilies.KnownTypes));
    }

    [Fact]
    public void An_error_value_has_its_own_family()
    {
        Assert.Equal(NodeFamily.Error, NodeFamilies.Of("Error"));
    }

    [Fact]
    public void Each_family_has_its_own_class()
    {
        var classes = NodeFamilies.All.Select(NodeFamilies.CssClass).ToList();

        Assert.Equal(classes.Count, classes.Distinct().Count());
    }
}
