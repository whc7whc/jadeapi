namespace Team.API.Payments
{
    public interface IPaymentGateway
    {
        Task<string> CreateAioCheckoutHtmlAsync(EcpayCreateOrder dto);
        bool VerifyCheckMac(IDictionary<string, string> fields);
    }
}
