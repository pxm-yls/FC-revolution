using System;
using FCRevolution.Contracts.Services;

namespace FC_Revolution.UI.AppServices;

public interface IBackendContractClient :
    IRomCatalogContract,
    IGameSessionContract,
    IRemoteControlContract,
    IDisposable
{
    Uri BaseAddress { get; }
}
