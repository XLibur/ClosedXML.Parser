using ClosedXML.Parser.Visualizer.Pages;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The app has one page, so the page is the root component and there is no Router. The Router
// would also make the trimmer report its NotFoundPage property.
builder.RootComponents.Add<Home>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

await builder.Build().RunAsync();
