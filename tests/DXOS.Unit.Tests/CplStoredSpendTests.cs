using DXOS.Application;
using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class CplStoredSpendTests
{
    private sealed class InMemoryLeadStore : ILeadStore
    {
        public List<Lead> Leads = [];
        public Task AddAsync(Lead lead, CancellationToken cancellationToken)
        {
            Leads.Add(lead);
            return Task.CompletedTask;
        }

        public Task<Lead?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Leads.FirstOrDefault(l => l.Id == id));
        }

        public Task<Lead?> FindByPhoneOrEmailAsync(string? phone, string? email, CancellationToken cancellationToken)
        {
            var match = Leads.FirstOrDefault(l =>
                (!string.IsNullOrWhiteSpace(phone) && l.Phone == phone) ||
                (!string.IsNullOrWhiteSpace(email) && l.Email == email));
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Lead>>(Leads);
        }

        public Task UpdateAsync(Lead lead, CancellationToken cancellationToken)
        {
            var idx = Leads.FindIndex(l => l.Id == lead.Id);
            if (idx >= 0) Leads[idx] = lead;
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Leads.Count);
        }

        public Task<IReadOnlyList<string>> ListSalesActorsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(["sales-1"]);
        }

        public Task RememberSalesActorAsync(string salesActor, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetLastAssignedSalesActorAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>("sales-1");
        }

        public Task SetLastAssignedSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task GetCpl_WhenSpendOverrideIsNull_UsesStoredSpend()
    {
        var leadStore = new InMemoryLeadStore();
        var leadService = new LeadService(leadStore, new FixedClock());

        await leadService.IntakeFormAsync("Customer A", "0901234567", "a@test.com", null, TestContext.Current.CancellationToken);
        await leadService.IntakeFormAsync("Customer B", "0901234568", "b@test.com", null, TestContext.Current.CancellationToken);

        var result = await leadService.GetCplAsync(
            spendOverride: null,
            dailySpend: 1_000_000,
            budget: 10_000_000,
            storedSpend: 4_000_000,
            TestContext.Current.CancellationToken);

        Assert.Equal(4_000_000m, result.Spend);
        Assert.Equal(2, result.LeadCount);
        Assert.Equal(2_000_000m, result.Cpl);
        Assert.Equal("VND", result.Currency);
    }

    [Fact]
    public async Task GetCpl_WhenSpendOverrideIsProvided_OverridesStoredSpend()
    {
        var leadStore = new InMemoryLeadStore();
        var leadService = new LeadService(leadStore, new FixedClock());

        await leadService.IntakeFormAsync("Customer A", "0901234567", "a@test.com", null, TestContext.Current.CancellationToken);

        var result = await leadService.GetCplAsync(
            spendOverride: 5_000_000,
            dailySpend: 1_000_000,
            budget: 10_000_000,
            storedSpend: 1_000_000,
            TestContext.Current.CancellationToken);

        Assert.Equal(5_000_000m, result.Spend);
        Assert.Equal(1, result.LeadCount);
        Assert.Equal(5_000_000m, result.Cpl);
    }
}
