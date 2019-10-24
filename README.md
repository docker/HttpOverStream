# HttpOverStream

[![NuGet version (HttpOverStream)](https://img.shields.io/nuget/v/HttpOverStream.svg)](https://www.nuget.org/packages/HttpOverStream/)

Used by Docker Desktop. (See http://github.com/docker/pinata)

.NET library for using HTTP 1.1 over streams, especially Windows Named Pipes.

This library essentially allows inter-process communication over named pipes using HTTP which doesn't require opening ports on the host machine like a standard web server.

There is both a client and server implementation and these have been tested across languages (GoLang and Javascript so far can both successfully send and receive messages here).
Server implementation in OWIN is more production ready than the .NET Core version.

Server usage (OWIN) is like this:
```
            var server = CustomListenerHost.Start(startupAction, new NamedPipeListener(pipeName));
```

Client usage:

```
    HttpClient = NamedPipeHttpClientFactory.ForPipeName(pipeName);
    
    var response = await HttpClient.GetAsync("/api/endpoint");
```
