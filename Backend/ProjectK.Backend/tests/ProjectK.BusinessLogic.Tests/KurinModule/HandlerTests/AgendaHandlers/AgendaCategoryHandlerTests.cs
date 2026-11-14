using FluentAssertions;
using Moq;
using ProjectK.BusinessLogic.Modules.KurinModule.Features.Agenda;
using ProjectK.Common.Contracts.KurinModule;
using ProjectK.Common.Contracts.Shared;
using ProjectK.Common.Entities.KurinModule.Agenda;
using ProjectK.Common.Enums.Shared;

namespace ProjectK.BusinessLogic.Tests.KurinModule.HandlerTests.AgendaHandlers;

public class AgendaCategoryHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAgendaCategoryRepository> _categories = new();
    private readonly Mock<IAgendaItemRepository> _items = new();
    private readonly Guid _kurinKey = Guid.NewGuid();

    public AgendaCategoryHandlerTests()
    {
        _uow.Setup(u => u.AgendaCategories).Returns(_categories.Object);
        _uow.Setup(u => u.AgendaItems).Returns(_items.Object);
    }

    private UpsertAgendaCategoryCommand ValidUpsert(Guid? key) => new()
    {
        AgendaCategoryKey = key,
        KurinKey = _kurinKey,
        Name = "Табір",
        ColorHex = "#2F855A",
        Icon = "pi pi-sun",
        Capacity = 20,
        WaitlistEnabled = true,
        RsvpRequired = true
    };

    [Fact]
    public async Task Upsert_WithoutKey_CreatesAndReturnsCreated()
    {
        var handler = new UpsertAgendaCategoryCommandHandler(_uow.Object);

        var result = await handler.Handle(ValidUpsert(key: null), default);

        result.Type.Should().Be(ResultType.Created);
        _categories.Verify(r => r.Create(It.Is<AgendaCategory>(c => c.KurinKey == _kurinKey && c.Name == "Табір"), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upsert_WithKeyInSameKurin_UpdatesAndReturnsSuccess()
    {
        var existing = new AgendaCategory { AgendaCategoryKey = Guid.NewGuid(), KurinKey = _kurinKey, Name = "old" };
        _categories.Setup(r => r.GetByKeyAsync(existing.AgendaCategoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var handler = new UpsertAgendaCategoryCommandHandler(_uow.Object);

        var result = await handler.Handle(ValidUpsert(existing.AgendaCategoryKey), default);

        result.Type.Should().Be(ResultType.Success);
        existing.Name.Should().Be("Табір");
        existing.Capacity.Should().Be(20);
        _categories.Verify(r => r.Update(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Upsert_ForCategoryInAnotherKurin_ReturnsForbidden()
    {
        var existing = new AgendaCategory { AgendaCategoryKey = Guid.NewGuid(), KurinKey = Guid.NewGuid(), Name = "old" };
        _categories.Setup(r => r.GetByKeyAsync(existing.AgendaCategoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        var handler = new UpsertAgendaCategoryCommandHandler(_uow.Object);

        var result = await handler.Handle(ValidUpsert(existing.AgendaCategoryKey), default);

        result.Type.Should().Be(ResultType.Forbidden);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Upsert_MissingCategory_ReturnsNotFound()
    {
        _categories.Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((AgendaCategory?)null);
        var handler = new UpsertAgendaCategoryCommandHandler(_uow.Object);

        var result = await handler.Handle(ValidUpsert(Guid.NewGuid()), default);

        result.Type.Should().Be(ResultType.NotFound);
    }

    [Fact]
    public async Task Delete_ClearsReferencingItemsThenRemoves()
    {
        var category = new AgendaCategory { AgendaCategoryKey = Guid.NewGuid(), KurinKey = _kurinKey };
        _categories.Setup(r => r.GetByKeyAsync(category.AgendaCategoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        var handler = new DeleteAgendaCategoryCommandHandler(_uow.Object);

        var result = await handler.Handle(new DeleteAgendaCategoryCommand(category.AgendaCategoryKey, _kurinKey), default);

        result.Type.Should().Be(ResultType.Success);
        _items.Verify(r => r.ClearCategoryAsync(category.AgendaCategoryKey, It.IsAny<CancellationToken>()), Times.Once);
        _categories.Verify(r => r.Delete(category, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_MissingCategory_ReturnsNotFound()
    {
        _categories.Setup(r => r.GetByKeyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((AgendaCategory?)null);
        var handler = new DeleteAgendaCategoryCommandHandler(_uow.Object);

        var result = await handler.Handle(new DeleteAgendaCategoryCommand(Guid.NewGuid(), _kurinKey), default);

        result.Type.Should().Be(ResultType.NotFound);
        _items.Verify(r => r.ClearCategoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_CategoryInAnotherKurin_ReturnsForbidden()
    {
        var category = new AgendaCategory { AgendaCategoryKey = Guid.NewGuid(), KurinKey = Guid.NewGuid() };
        _categories.Setup(r => r.GetByKeyAsync(category.AgendaCategoryKey, It.IsAny<CancellationToken>())).ReturnsAsync(category);
        var handler = new DeleteAgendaCategoryCommandHandler(_uow.Object);

        var result = await handler.Handle(new DeleteAgendaCategoryCommand(category.AgendaCategoryKey, _kurinKey), default);

        result.Type.Should().Be(ResultType.Forbidden);
        _items.Verify(r => r.ClearCategoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
