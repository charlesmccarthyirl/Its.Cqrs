namespace Sample.Domain
{
    public partial class CustomerAccount
    {
        public class OrderShipConfirmationEmailSent : EmailSent
        {
            public string OrderNumber { get; set; }

            public override void Update(CustomerAccount aggregate)
            {
                aggregate.CommunicationsSent.Add(string.Format("Your order has shipped! (Order #{0})", OrderNumber));
            }
        }
    }
}