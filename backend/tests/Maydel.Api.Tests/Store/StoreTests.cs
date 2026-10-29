using System.Net;
using System.Net.Http.Json;
using Maydel.Api.Tests.Infrastructure;
using Maydel.Application.Academics;
using Maydel.Application.Common;
using Maydel.Application.Common.Messaging;
using Maydel.Application.Identity;
using Maydel.Application.Notifications;
using Maydel.Application.Store;
using Maydel.Application.Students;
using Maydel.Domain.Academics;
using Maydel.Domain.Common;
using Maydel.Domain.Communication;
using Maydel.Domain.Store;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig;

namespace Maydel.Api.Tests.Store;

/// <summary>E19: the catalogue and stock, office sales, and the parent shop (stock held, ready, collected, released).</summary>
[Collection(nameof(ApiCollection))]
public sealed class StoreTests(PostgresFixture fixture)
{
    private ApiFactory Api => fixture.Api;
    private static int _phone = Random.Shared.Next(1_000_000, 8_000_000);

    [Fact]
    public async Task The_bursar_prices_restocks_and_sells_with_every_change_logged()
    {
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var starter = await ReadAsync<List<StoreItemDto>>(await bursar.GetAsync("/api/store/items"));
        Assert.Contains(starter, i => i.Name == "Blazer (black)" && !i.IsActive); // the starter list waits for prices

        await (await bursar.PostAsJsonAsync("/api/store/items", Item("Unpriced", isActive: true, price: 0))).AssertProblemAsync(HttpStatusCode.BadRequest, "validation");
        var tie = await ReadAsync<StoreItemDto>(await bursar.PostAsJsonAsync("/api/store/items", Item($"Tie {Guid.NewGuid():N}"[..12], isActive: true, price: 2500)));
        var variant = Assert.Single(tie.Variants);
        Assert.True(variant.IsLow); // nothing in stock yet
        Assert.Equal(tie.Name, (await ReadAsync<StoreItemDto>(await bursar.GetAsync($"/api/store/items/{tie.Id}"))).Name);
        await (await bursar.GetAsync($"/api/store/items/{Guid.NewGuid()}")).AssertProblemAsync(HttpStatusCode.NotFound, "store.item_not_found");

        var stocked = await ReadAsync<VariantDto>(await bursar.PostAsJsonAsync($"/api/store/variants/{variant.Id}/restock", new { quantity = 10, unitCost = 1500, supplier = "Abuja Uniforms" }));
        Assert.Equal((10, false), (stocked.Stock, stocked.IsLow));
        await (await bursar.PostAsJsonAsync($"/api/store/variants/{variant.Id}/adjust", new { change = -1, reason = "" })).AssertProblemAsync(HttpStatusCode.BadRequest);
        Assert.Equal(9, (await ReadAsync<VariantDto>(await bursar.PostAsJsonAsync($"/api/store/variants/{variant.Id}/adjust", new { change = -1, reason = "Damaged" }))).Stock);

        var s = await SchoolBuilder.SetUpAsync(Api);
        var sale = await ReadAsync<StoreOrderDto>(await bursar.PostAsJsonAsync("/api/store/orders", new
        {
            studentId = s.Students[0], lines = new[] { new { variantId = variant.Id, quantity = 2 } }, payment = "Cash",
        }));
        Assert.Equal((OrderStatus.Collected, OrderPayment.Paid, 5000m), (sale.Status, sale.Payment, sale.Total));
        Assert.StartsWith("ORD-", sale.Number);
        await (await bursar.PostAsJsonAsync("/api/store/orders", new { studentId = s.Students[0], lines = new[] { new { variantId = variant.Id, quantity = 8 } }, payment = "Cash" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "store.out_of_stock");
        await (await bursar.PostAsJsonAsync("/api/store/orders", new { studentId = s.Students[0], lines = new[] { new { variantId = variant.Id, quantity = 1 } }, payment = "Fees" }))
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "store.no_invoice"); // no invoice this term for this new student

        var moves = await ReadAsync<List<MovementDto>>(await bursar.GetAsync($"/api/store/variants/{variant.Id}/movements"));
        Assert.Equal(new[] { MovementKind.Sale, MovementKind.Adjustment, MovementKind.Restock }, moves.Select(m => m.Kind).ToArray());
        Assert.Equal(sale.Number, moves[0].OrderNumber);
        await (await (await Api.ClientAsync(Roles.Teacher)).GetAsync("/api/store/items")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_parent_orders_for_their_child_and_collects_once_it_is_paid()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var parent = await ParentAsync(s.Students[0]);
        var child = s.Students[0];

        // One item for everyone, one only for another section.
        var section = (await ReadAsync<List<ClassRoomDto>>(await bursar.GetAsync("/api/academics/classrooms"))).Single(r => r.Id == s.ClassRoomId).Section;
        var otherSection = section == SchoolSection.SeniorSecondary ? StoreSections.Creche : StoreSections.SeniorSecondary;
        var shirt = await ReadAsync<StoreItemDto>(await bursar.PostAsJsonAsync("/api/store/items", Item($"Shirt {Guid.NewGuid():N}"[..14], isActive: true, price: 4000)));
        var hidden = await ReadAsync<StoreItemDto>(await bursar.PostAsJsonAsync("/api/store/items", Item($"Other {Guid.NewGuid():N}"[..14], isActive: true, price: 9000, otherSection)));
        var variant = shirt.Variants[0].Id;
        (await bursar.PostAsJsonAsync($"/api/store/variants/{variant}/restock", new { quantity = 3 })).EnsureSuccessStatusCode();

        var shop = await ReadAsync<ShopCatalogue>(await parent.GetAsync($"/api/portal/children/{child}/shop"));
        Assert.Contains(shop.Items, i => i.Id == shirt.Id);
        Assert.DoesNotContain(shop.Items, i => i.Id == hidden.Id);
        await (await parent.PostAsJsonAsync($"/api/portal/children/{child}/shop/orders", new { lines = new[] { new { variantId = hidden.Variants[0].Id, quantity = 1 } }, payment = "AtOffice" }))
            .AssertProblemAsync(HttpStatusCode.NotFound);

        var order = await ReadAsync<StoreOrderDto>(await parent.PostAsJsonAsync($"/api/portal/children/{child}/shop/orders", new { lines = new[] { new { variantId = variant, quantity = 2 } }, payment = "AtOffice" }));
        Assert.Equal((OrderStatus.Placed, OrderPayment.Unpaid, true), (order.Status, order.Payment, order.HoldUntil is not null));
        Assert.Equal(1, Stock(await ReadAsync<List<StoreItemDto>>(await bursar.GetAsync("/api/store/items")), variant)); // held for the order

        // The office packs it; the parent is told; it can't go out unpaid.
        (await bursar.PostAsync($"/api/store/orders/{order.Id}/ready", null)).EnsureSuccessStatusCode();
        Assert.Contains((await parent.GetFromJsonAsync<NotificationInbox>("/api/notifications", Json.Options))!.Items, n => n.Kind == NotificationKind.StoreOrderReady);
        await (await bursar.PostAsync($"/api/store/orders/{order.Id}/collected", null)).AssertProblemAsync(HttpStatusCode.UnprocessableEntity, "order.unpaid");
        (await bursar.PostAsJsonAsync($"/api/store/orders/{order.Id}/pay", new { method = "Pos", reference = "POS-11" })).EnsureSuccessStatusCode();
        var done = await ReadAsync<StoreOrderDto>(await bursar.PostAsync($"/api/store/orders/{order.Id}/collected", null));
        Assert.Equal((OrderStatus.Collected, OrderPayment.Paid), (done.Status, done.Payment));

        // A notification link carries only the order id: the family can open it, nobody else can.
        Assert.Equal(OrderStatus.Collected, (await ReadAsync<StoreOrderDto>(await parent.GetAsync($"/api/portal/shop/orders/{order.Id}"))).Status);
        var stranger = await ParentAsync((await SchoolBuilder.SetUpAsync(Api)).Students[0]);
        await (await stranger.GetAsync($"/api/portal/shop/orders/{order.Id}")).AssertProblemAsync(HttpStatusCode.NotFound, "store.order_not_found");

        // A second order the parent cancels gives its stock back; a third that's never paid is released when the hold runs out.
        var second = await ReadAsync<StoreOrderDto>(await parent.PostAsJsonAsync($"/api/portal/children/{child}/shop/orders", new { lines = new[] { new { variantId = variant, quantity = 1 } }, payment = "AtOffice" }));
        Assert.Equal(OrderStatus.Cancelled, (await ReadAsync<StoreOrderDto>(await parent.PostAsync($"/api/portal/children/{child}/shop/orders/{second.Id}/cancel", null))).Status);
        var third = await ReadAsync<StoreOrderDto>(await parent.PostAsJsonAsync($"/api/portal/children/{child}/shop/orders", new { lines = new[] { new { variantId = variant, quantity = 1 } }, payment = "AtOffice" }));
        Assert.True(await ReleaseAsync(third.HoldUntil!.Value.AddMinutes(1)) >= 1);
        Assert.Equal(OrderStatus.Cancelled, (await ReadAsync<List<StoreOrderDto>>(await parent.GetAsync($"/api/portal/children/{child}/shop/orders"))).Single(o => o.Id == third.Id).Status);
        Assert.Equal(1, Stock(await ReadAsync<List<StoreItemDto>>(await bursar.GetAsync("/api/store/items")), variant));
    }

    [Fact]
    public async Task Only_the_child_s_parents_can_shop_for_them()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var other = await SchoolBuilder.SetUpAsync(Api);
        var stranger = await ParentAsync(other.Students[0]);
        await (await stranger.GetAsync($"/api/portal/children/{s.Students[0]}/shop")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await stranger.PostAsJsonAsync($"/api/portal/children/{s.Students[0]}/shop/orders", new { lines = Array.Empty<object>(), payment = "AtOffice" }))
            .AssertProblemAsync(HttpStatusCode.NotFound);
        await (await (await Api.ClientAsync(Roles.Bursar)).PutAsJsonAsync("/api/store/settings", new { parentsCanChargeToFees = false, holdHours = 24 }))
            .AssertProblemAsync(HttpStatusCode.Forbidden); // settings are the admin's
    }

    [Fact]
    public async Task Receipts_print_for_the_office_and_the_family_and_id_cards_print_for_a_class()
    {
        var s = await SchoolBuilder.SetUpAsync(Api);
        var bursar = await Api.ClientAsync(Roles.Bursar);
        var admin = await Api.ClientAsync(Roles.Admin);
        var parent = await ParentAsync(s.Students[0]);
        var item = await ReadAsync<StoreItemDto>(await bursar.PostAsJsonAsync("/api/store/items", Item($"Book {Guid.NewGuid():N}"[..13], isActive: true, price: 3500)));
        (await bursar.PostAsJsonAsync($"/api/store/variants/{item.Variants[0].Id}/restock", new { quantity = 5 })).EnsureSuccessStatusCode();
        var order = await ReadAsync<StoreOrderDto>(await parent.PostAsJsonAsync($"/api/portal/children/{s.Students[0]}/shop/orders",
            new { lines = new[] { new { variantId = item.Variants[0].Id, quantity = 2 } }, payment = "AtOffice" }));

        var office = await PdfTextAsync(await bursar.GetAsync($"/api/store/orders/{order.Id}/receipt.pdf"));
        Assert.Contains(order.Number, office);
        Assert.Contains("NGN 7,000.00", office);
        Assert.Contains("UNPAID", office);
        Assert.Contains(order.Number, await PdfTextAsync(await parent.GetAsync($"/api/portal/children/{s.Students[0]}/shop/orders/{order.Id}/receipt.pdf")));
        var other = await SchoolBuilder.SetUpAsync(Api);
        var stranger = await ParentAsync(other.Students[0]);
        await (await stranger.GetAsync($"/api/portal/children/{s.Students[0]}/shop/orders/{order.Id}/receipt.pdf")).AssertProblemAsync(HttpStatusCode.NotFound);
        await (await stranger.GetAsync($"/api/portal/children/{other.Students[0]}/shop/orders/{order.Id}/receipt.pdf")).AssertProblemAsync(HttpStatusCode.NotFound);

        // ID cards: one, or a whole class on A4 sheets; admin only.
        var card = await PdfTextAsync(await admin.GetAsync($"/api/students/{s.Students[0]}/id-card.pdf"));
        Assert.Contains("STUDENT IDENTITY CARD", card);
        var sheet = await admin.GetAsync($"/api/academics/classrooms/{s.ClassRoomId}/id-cards.pdf");
        using (var pdf = PdfDocument.Open(await sheet.Content.ReadAsByteArrayAsync()))
            Assert.Equal(s.Students.Count, pdf.GetPages().Sum(p => System.Text.RegularExpressions.Regex.Matches(p.Text, "STUDENT IDENTITY CARD").Count));
        await (await bursar.GetAsync($"/api/students/{s.Students[0]}/id-card.pdf")).AssertProblemAsync(HttpStatusCode.Forbidden);
    }

    private static async Task<string> PdfTextAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        return string.Join('\n', pdf.GetPages().Select(p => p.Text));
    }

    // ---------- helpers ----------

    private static object Item(string name, bool isActive, decimal price, StoreSections sections = StoreSections.None) => new
    {
        name, category = "Uniform", sections = sections.ToString(), isActive, variants = new[] { new { label = "Standard", price, lowStockAt = 2 } },
    };

    private static int Stock(List<StoreItemDto> items, Guid variant) => items.SelectMany(i => i.Variants).Single(v => v.Id == variant).Stock;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode) Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }

    private async Task<int> ReleaseAsync(DateTimeOffset now)
    {
        await using var scope = Api.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<ICommandHandler<ReleaseExpiredOrdersCommand, int>>().Handle(new ReleaseExpiredOrdersCommand(now), CancellationToken.None)).Value;
    }

    private async Task<HttpClient> ParentAsync(Guid studentId)
    {
        var admin = await Api.ClientAsync(Roles.Admin);
        var added = await admin.PostAsJsonAsync($"/api/students/{studentId}/guardians", new
        {
            fullName = "Mr. Shop Parent", phone = $"0803{Interlocked.Increment(ref _phone):0000000}", email = $"shop-{Guid.NewGuid():N}@example.com",
            relationship = "Father", isPrimaryContact = true, canPickUp = true,
        });
        var guardianId = (await added.Content.ReadFromJsonAsync<GuardianAdded>(Json.Options))!.GuardianId;
        var issued = (await (await admin.PostAsync($"/api/guardians/{guardianId}/login", null)).Content.ReadFromJsonAsync<GuardianLoginIssued>(Json.Options))!;
        var first = await Api.LoginAsync(issued.Credentials!.UserName, issued.Credentials.TemporaryPassword);
        var changed = await Api.CreateClient(first.AccessToken).PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = issued.Credentials.TemporaryPassword, newPassword = "Family#NewPass2026" });
        return Api.CreateClient((await changed.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!.AccessToken);
    }
}
