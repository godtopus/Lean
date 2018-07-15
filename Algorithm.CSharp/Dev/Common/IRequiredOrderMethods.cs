using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public interface IRequiredOrderMethods
    {
        OrderTicket StopMarketOrder(Symbol symbol, int quantity, decimal stopPrice, string tag = "");
        OrderTicket MarketOrder(Symbol symbol, int quantity, bool asynchronous = false, string tag = "");
        OrderTicket LimitOrder(Symbol symbol, int quantity, decimal limitPrice, string tag = "");
    }
}