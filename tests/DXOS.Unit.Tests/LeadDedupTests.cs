using DXOS.Application;
using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class LeadDedupTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Intake_SamePhone_UpdatesExistingLead_WithoutCreatingNew()
    {
        var clock = new FixedClock(T0);
        var store = new InMemoryLeadStore(["sales-1"]);
        var service = new LeadService(store, clock);

        var first = await service.IntakeFormAsync("Khach Hang 1", "0901234567", null, null, CancellationToken.None);
        Assert.Single(store.Leads);
        Assert.Single(first.Sources);
        Assert.Equal(LeadSource.Form, first.Sources[0]);

        // Second intake with same phone but via Message
        var second = await service.RecordMessageOrCallAsync("Khach Hang 1 (Updated)", "0901234567", "khach1@test.vn", LeadSource.Message, null, CancellationToken.None);

        Assert.Single(store.Leads);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, second.Sources.Count);
        Assert.Contains(LeadSource.Form, second.Sources);
        Assert.Contains(LeadSource.Message, second.Sources);
    }

    [Fact]
    public async Task Intake_SameEmail_UpdatesExistingLead()
    {
        var clock = new FixedClock(T0);
        var store = new InMemoryLeadStore(["sales-1"]);
        var service = new LeadService(store, clock);

        var first = await service.IntakeFormAsync("Khach Hang", null, "khach@company.vn", null, CancellationToken.None);
        Assert.Single(store.Leads);

        var second = await service.RecordMessageOrCallAsync("Khach Hang 2", "0909999999", "khach@company.vn", LeadSource.Call, null, CancellationToken.None);

        Assert.Single(store.Leads);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, second.Sources.Count);
        Assert.Contains(LeadSource.Form, second.Sources);
        Assert.Contains(LeadSource.Call, second.Sources);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class InMemoryLeadStore : ILeadStore
    {
        public readonly List<Lead> Leads = [];
        private readonly List<string> _sales;
        private string? _lastAssigned;

        public InMemoryLeadStore(IEnumerable<string> sales) => _sales = sales.ToList();

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
            return Task.FromResult<IReadOnlyList<string>>(_sales);
        }

        public Task RememberSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<string?> GetLastAssignedSalesActorAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_lastAssigned);
        }

        public Task SetLastAssignedSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            _lastAssigned = actorId;
            return Task.CompletedTask;
        }
    }
}
