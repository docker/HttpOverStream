# HttpOverStream
[![NuGet](https://img.shields.io/nuget/v/HttpOverStream?color=green)](https://www.nuget.org/packages/HttpOverStream/)
[![Build Status](https://ci-next.docker.com/public/job/HttpOverStream/job/master/badge/icon)](https://ci-next.docker.com/public/job/HttpOverStream/job/master/)

Used by Docker Desktop.

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
    _httpClient = new NamedPipeHttpClientBuilder("myPipeName")
                    .WithPerRequestTimeout(TimeSpan.FromSeconds(5))
                    .Build();
    var response = await _httpClient.GetAsync("/api/endpoint");
```
