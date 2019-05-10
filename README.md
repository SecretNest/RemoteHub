
# Remote Hub
Network communicating solution for [Remote Agency](https://secretnest.info/RemoteAgency).

Home page: https://secretnest.info/RemoteHub

Documentation: https://scegg.github.io/RemoteHub/

# Nuget Packages
|Package|Description|
|---|---|
|[SecretNest.IRemoteHub](https://www.nuget.org/packages/SecretNest.IRemoteHub)|Interfaces, delegates and shared classes of Remote Hub.|
|[SecretNest.IRemoteHub.Redis](https://www.nuget.org/packages/SecretNest.IRemoteHub.Redis)|Interfaces of Remote Hub based on Redis database.|
|[SecretNest.IRemoteHub.Stream](https://www.nuget.org/packages/SecretNest.IRemoteHub.Stream)|Interfaces of Remote Hub based on streams.|
|[SecretNest.RemoteHub.RedisAdapter](https://www.nuget.org/packages/SecretNest.RemoteHub.RedisAdapter)|Adapter of Remote Hub based on Redis database.|
|[SecretNest.RemoteHub.Redis](https://www.nuget.org/packages/SecretNest.RemoteHub.Redis)|Remote Hub based on Redis database.|
|[SecretNest.RemoteHub.StreamAdapter](https://www.nuget.org/packages/SecretNest.RemoteHub.StreamAdapter)|Adapter of Remote Hub based on streams.|
|[SecretNest.RemoteHub.Stream](https://www.nuget.org/packages/SecretNest.RemoteHub.Stream)|Remote Hub based on streams.|
|[SecretNest.RemoteHub.SwitchDirect](https://www.nuget.org/packages/SecretNest.RemoteHub.SwitchDirect)|Remote Hub for attaching to local switch instance directly.|
|[SecretNest.RemoteHub.Switch](https://www.nuget.org/packages/SecretNest.RemoteHub.Switch)|A switch for connecting adapters and routing messages.|

# Remote Agency
[Remote Agency](https://secretnest.info/RemoteAgency) is built for making the communicating among components in different computers easier. [Remote Agency](https://secretnest.info/RemoteAgency) can create proxy objects based on one interface file which should be implemented by a remote class, and serializing the accessing between the proxy and the real service object.