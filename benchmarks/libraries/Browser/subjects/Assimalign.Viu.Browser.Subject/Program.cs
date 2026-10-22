using Assimalign.Viu;
using Assimalign.Viu.Browser;
using Assimalign.Viu.Components;
using Assimalign.Viu.Browser.Subject;

await using BrowserApplication application = new BrowserApplicationBuilder()
    .ConfigureApplication(options => options.RootComponent = ComponentNode.For<App>())
    .ConfigureBrowser(options => options.MountTargetSelector = "#app")
    .Build();
await application.RunAsync();