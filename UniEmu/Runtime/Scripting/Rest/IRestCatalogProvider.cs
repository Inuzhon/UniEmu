namespace UniEmu.Runtime.Scripting.Rest;

internal interface IRestCatalogProvider
{
    RestCatalogSnapshot GetSnapshot();
}
