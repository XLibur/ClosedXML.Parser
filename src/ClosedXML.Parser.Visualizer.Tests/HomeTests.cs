using Bunit;
using Bunit.TestDoubles;
using ClosedXML.Parser.Visualizer.Components;
using ClosedXML.Parser.Visualizer.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace ClosedXML.Parser.Visualizer.Tests;

public class HomeTests : BunitContext
{
    public HomeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./Components/MermaidDiagram.razor.js");
        module.Mode = JSRuntimeMode.Loose;
        module.Setup<MermaidDiagram.RenderResult?>("render", _ => true).SetResult(new MermaidDiagram.RenderResult(false, null));
    }

    private BunitNavigationManager Navigation => Services.GetRequiredService<BunitNavigationManager>();

    [Fact]
    public void Shows_the_default_formula()
    {
        var cut = Render<Home>();

        Assert.Equal(Home.DefaultFormula, cut.Find("#formula").GetAttribute("value"));
        Assert.Contains("n0[\"SUM<br/>[Function]\"]", Diagram(cut).Definition);
        Assert.Equal("true", StyleButton(cut, "A1").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Reads_the_formula_and_the_style_from_the_url()
    {
        Navigation.NavigateTo("?f=SUM(R2C3)&style=R1C1");

        var cut = Render<Home>();

        Assert.Equal("SUM(R2C3)", cut.Find("#formula").GetAttribute("value"));
        Assert.Equal("true", StyleButton(cut, "R1C1").GetAttribute("aria-pressed"));
        Assert.Contains("[Reference]", Diagram(cut).Definition);
    }

    [Fact]
    public void Typing_parses_after_a_pause_and_replaces_the_url()
    {
        var cut = Render<Home>();

        cut.Find("#formula").Input("AVERAGE(A1:A3)");

        cut.WaitForAssertion(() => Assert.Contains("AVERAGE<br/>[Function]", Diagram(cut).Definition));
        Assert.Equal("AVERAGE(A1:A3)", QueryValue(Navigation.Uri, "f"));
        Assert.True(Navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public void An_error_keeps_the_last_tree_dimmed_and_marks_the_position()
    {
        var cut = Render<Home>();
        var lastTree = Diagram(cut).Definition;

        cut.Find("#formula").Input("SUM(B5,");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.Find("[role=alert]").TextContent));
        Assert.Equal(lastTree, Diagram(cut).Definition);
        Assert.True(Diagram(cut).Dimmed);
        // The formula ended too soon, so the mark is after the last character.
        Assert.Equal(" ", cut.Find("[data-segment=error]").TextContent);
    }

    [Fact]
    public void An_example_sets_the_formula_and_the_style()
    {
        var cut = Render<Home>();

        cut.FindAll("[aria-label=Examples] button").Single(button => button.TextContent == "R1C1").Click();

        var example = ExampleFormulas.All.Single(example => example.Label == "R1C1");
        Assert.Equal(example.Formula, cut.Find("#formula").GetAttribute("value"));
        Assert.Equal("true", StyleButton(cut, "R1C1").GetAttribute("aria-pressed"));
        Assert.Equal("R1C1", QueryValue(Navigation.Uri, "style"));
    }

    [Fact]
    public async Task The_style_switch_parses_the_formula_again()
    {
        var cut = Render<Home>(); // SUM(B5,2)
        var a1Tree = Diagram(cut).Definition;

        cut.Find("#formula").Input("R2C3");
        cut.WaitForAssertion(() => Assert.Contains("[Name]", Diagram(cut).Definition));
        // The URL update renders the page again, so find and click the button in one step.
        await cut.InvokeAsync(() => StyleButton(cut, "R1C1").Click());

        Assert.Contains("[Reference]", Diagram(cut).Definition);
        Assert.NotEqual(a1Tree, Diagram(cut).Definition);
        Assert.Equal("R1C1", QueryValue(Navigation.Uri, "style"));
    }

    [Fact]
    public async Task Clicking_a_node_highlights_its_text()
    {
        var cut = Render<Home>(); // SUM(B5,2)

        await cut.InvokeAsync(() => Diagram(cut).NodeClicked("n1"));

        Assert.Equal("B5", cut.Find("[data-segment=highlight]").TextContent);
        Assert.Contains("[4:6]", cut.Find("[data-testid=node-details]").TextContent);
    }

    [Fact]
    public async Task Clicking_the_selected_node_again_clears_the_selection()
    {
        var cut = Render<Home>();

        await cut.InvokeAsync(() => Diagram(cut).NodeClicked("n1"));
        await cut.InvokeAsync(() => Diagram(cut).NodeClicked("n1"));

        Assert.Empty(cut.FindAll("[data-segment=highlight]"));
    }

    private static MermaidDiagram Diagram(IRenderedComponent<Home> cut) => cut.FindComponent<MermaidDiagram>().Instance;

    private static AngleSharp.Dom.IElement StyleButton(IRenderedComponent<Home> cut, string style) =>
        cut.FindAll("[aria-label='Reference style'] button").Single(button => button.TextContent == style);

    private static string? QueryValue(string uri, string name)
    {
        var query = new Uri(uri).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == name)
                return parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty;
        }

        return null;
    }
}
