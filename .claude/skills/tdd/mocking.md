# When to Substitute

Examples use [NSubstitute](https://nsubstitute.github.io). "Substitute" is NSubstitute's word for what other frameworks call a mock, stub, or fake; the rules below are the same whatever you call it.

Substitute at **system boundaries** only:

- External APIs (payment, email, etc.)
- Databases (sometimes — prefer a real test database)
- Time and randomness (inject `TimeProvider`, not `DateTime.Now`)
- File system (sometimes — `IFileSystem` from `System.IO.Abstractions`)

Don't substitute:

- Your own classes
- Internal collaborators
- Anything you control

## Designing for substitutability

At system boundaries, design interfaces that are easy to substitute:

**1. Use dependency injection**

Pass external dependencies in rather than creating them internally:

```csharp
// Easy to substitute
public sealed class PaymentProcessor(IPaymentClient client)
{
    public Task<Receipt> ProcessAsync(Order order) => client.ChargeAsync(order.Total);
}

// Hard to substitute
public sealed class PaymentProcessor
{
    public Task<Receipt> ProcessAsync(Order order)
    {
        var client = new StripeClient(Environment.GetEnvironmentVariable("STRIPE_KEY"));
        return client.ChargeAsync(order.Total);
    }
}
```

**2. Prefer narrow, operation-per-member interfaces over a generic sender**

Declare a member for each external operation instead of one generic member with conditional logic behind it:

```csharp
// GOOD: Each member is independently substitutable
public interface IBillingApi
{
    Task<User> GetUserAsync(UserId id);
    Task<IReadOnlyList<Order>> GetOrdersAsync(UserId userId);
    Task<Order> CreateOrderAsync(NewOrder order);
}

// BAD: Substituting requires conditional logic inside the substitute
public interface IHttpGateway
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
}
```

The narrow approach means:

- Each configured return value is one specific shape
- No conditional logic in test setup
- Easier to see which operations a test exercises
- Type safety per operation

```csharp
var billing = Substitute.For<IBillingApi>();
billing.GetUserAsync(userId).Returns(new User(userId, "Alice"));
```

## NSubstitute specifics

- `Substitute.For<T>()` needs an **interface**, or a class whose members are `virtual` or `abstract`. Sealed classes, non-virtual members, and statics can't be intercepted, so a dependency you intend to substitute has to be declared as an interface.
- Configure return values (`.Returns(...)`) rather than asserting on calls. Assert with `Received()` only when the call **is** the observable behavior: a real effect crossing the boundary that has no return value to check, like "the confirmation email was sent". Verifying an internal call is the anti-pattern in [tests.md](tests.md).
- A substitute returns `null`, `0`, or an empty collection for anything you didn't configure, so a test can pass while exercising a path you never set up. Configure exactly what the test needs and no more.
