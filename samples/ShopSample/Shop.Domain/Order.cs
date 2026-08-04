using Shop.Infrastructure;

namespace Shop.Domain;

public sealed class Order
{
    public PaymentRecord? Payment { get; set; }
}
