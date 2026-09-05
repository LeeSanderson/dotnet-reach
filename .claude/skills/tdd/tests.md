# Good and Bad Tests

Examples use xUnit, [AwesomeAssertions](https://awesomeassertions.org) for assertions, and [NSubstitute](https://nsubstitute.github.io) for substitutes.

## Good Tests

**Integration-style**: Test through real interfaces, not substitutes for internal parts.

```csharp
// GOOD: Tests observable behavior
[Fact]
public async Task User_can_checkout_with_valid_cart()
{
    var cart = new Cart();
    cart.Add(product);

    var result = await checkout.PlaceAsync(cart, paymentMethod);

    result.Status.Should().Be(CheckoutStatus.Confirmed);
}
```

Characteristics:

- Tests behavior users/callers care about
- Uses public API only
- Survives internal refactors
- Describes WHAT, not HOW
- One logical assertion per test

## Bad Tests

**Implementation-detail tests**: Coupled to internal structure.

```csharp
// BAD: Tests implementation details
[Fact]
public async Task Checkout_calls_payment_service_process()
{
    var payments = Substitute.For<IPaymentService>();
    var checkout = new Checkout(payments);

    await checkout.PlaceAsync(cart, paymentMethod);

    await payments.Received(1).ProcessAsync(cart.Total);
}
```

Red flags:

- Substituting internal collaborators
- Testing private members (or reaching them through `InternalsVisibleTo` / reflection)
- Asserting on call counts/order with `Received()`
- Test breaks when refactoring without behavior change
- Test name describes HOW not WHAT
- Verifying through external means instead of interface

```csharp
// BAD: Bypasses interface to verify
[Fact]
public async Task CreateUser_saves_to_database()
{
    await users.CreateAsync(new NewUser("Alice"));

    var row = await connection.QuerySingleOrDefaultAsync<UserRow>(
        "SELECT * FROM Users WHERE Name = @Name", new { Name = "Alice" });
    row.Should().NotBeNull();
}

// GOOD: Verifies through interface
[Fact]
public async Task CreateUser_makes_user_retrievable()
{
    var user = await users.CreateAsync(new NewUser("Alice"));

    var retrieved = await users.GetAsync(user.Id);
    retrieved.Name.Should().Be("Alice");
}
```

**Tautological tests**: Expected value restates the implementation, so the test passes by construction.

```csharp
// BAD: Expected value is recomputed the way the code computes it
[Fact]
public void CalculateTotal_sums_line_items()
{
    var items = new[] { new LineItem(10m), new LineItem(5m) };
    var expected = items.Sum(i => i.Price);

    Invoice.CalculateTotal(items).Should().Be(expected);
}

// GOOD: Expected value is an independent, known literal
[Fact]
public void CalculateTotal_sums_line_items()
{
    var items = new[] { new LineItem(10m), new LineItem(5m) };

    Invoice.CalculateTotal(items).Should().Be(15m);
}
```

`[Theory]` with `[InlineData]` is the same trap in bulk: the cases are only worth having if each expected value is an independent literal, not a formula applied to the input.
