using HttpOverStream.Client;
using HttpOverStream.NamedPipe;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HttpOverStream.Server.AspnetCore.Tests
{
    public class PersonMessage
    {
        public string Name { get; set; }
    }

    public class WelcomeMessage
    {
        public string Text { get; set; }
    }
    [Route("api/e2e-tests")]
    public class EndToEndApiController : ControllerBase
    {
        [HttpGet("hello-world")]
        public string HelloWorld()
        {
            return "Hello World";
        }

        [HttpPost("hello")]
        public WelcomeMessage Hello([FromBody] PersonMessage person)
        {
            return new WelcomeMessage { Text = $"Hello {person.Name}" };
        }
    }
    public class EndToEndTests
    {
        [Fact]
        public async Task TestHelloWorld()
        {
            var builder = new WebHostBuilder().Configure(app => app.UseMvc())
                .ConfigureServices(svc => svc.AddMvc().AddApplicationPart(Assembly.GetAssembly(typeof(EndToEndApiController))))
                .UseServer(new CustomListenerHost(new NamedPipeListener("test-core-get")));

            var host = builder.Build();
            await host.StartAsync();

            var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer("test-core-get")));
            var result = await client.GetStringAsync("http://localhost/api/e2e-tests/hello-world");
            Assert.Equal("Hello World", result);

            await host.StopAsync();

        }
        [Fact]
        public async Task TestHelloPost()
        {
            var builder = new WebHostBuilder().Configure(app => app.UseMvc())
                .ConfigureServices(svc => svc.AddMvc().AddApplicationPart(Assembly.GetAssembly(typeof(EndToEndApiController))))
                .UseServer(new CustomListenerHost(new NamedPipeListener("test-core-post")));

            var host = builder.Build();
            await host.StartAsync();

            var client = new HttpClient(new DialMessageHandler(new NamedPipeDialer("test-core-post")));
            var result = await client.PostAsJsonAsync("http://localhost/api/e2e-tests/hello", new PersonMessage { Name = "Test" });
            var wlcMsg = await result.Content.ReadAsAsync<WelcomeMessage>();
            Assert.Equal("Hello Test", wlcMsg.Text);

            await host.StopAsync();

        }
    }
}
