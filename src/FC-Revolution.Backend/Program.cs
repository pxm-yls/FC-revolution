using FCRevolution.Backend.Abstractions;
using FCRevolution.Backend.Hosting;
using FCRevolution.Backend.Services;

var host = new BackendHostService(new BackendHostOptions(11778), new NullBackendRuntimeBridge());
await host.StartAsync();
await Task.Delay(Timeout.InfiniteTimeSpan);
