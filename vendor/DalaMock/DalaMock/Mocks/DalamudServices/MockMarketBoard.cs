namespace DalaMock.Core.Mocks.DalamudServices;

public class MockMarketBoard : IMarketBoard, IMockService
{
    public event IMarketBoard.HistoryReceivedDelegate? HistoryReceived;

    public event IMarketBoard.ItemPurchasedDelegate? ItemPurchased;

    public event IMarketBoard.OfferingsReceivedDelegate? OfferingsReceived;

    public event IMarketBoard.PurchaseRequestedDelegate? PurchaseRequested;

    public event IMarketBoard.TaxRatesReceivedDelegate? TaxRatesReceived;

    public string ServiceName { get; } = "IMarketBoard";
}
