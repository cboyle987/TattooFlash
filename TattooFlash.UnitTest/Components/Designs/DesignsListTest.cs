using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Extensions;
using TattooFlash.Web.Components.Designs;
using TattooFlash.Web.Components.Placeholders;
using TattooFlash.Web.Interfaces;
using TattooFlash.Web.Models;

namespace TattooFlash.UnitTest.Components.Designs
{
    public class DesignsListTest
    {
        [Fact]
        public async Task WhenServiceRequestIsPendingThenPlaceholdersAreDisplayed()
        {
            // Arrange
            var pending = CreatePendingRequest();
            var designService = new Mock<IDesignService>(MockBehavior.Strict);
            designService.Setup(x => x.GetPaginatedDesigns(1)).Returns(pending.Task);
            await using var context = CreateContext(designService);

            // Act
            var page = context.RenderWithProviders<DesignsList>();

            // Assert
            AssertLoading(page);

            pending.SetResult(CreatePage(1, totalItems: 0));
            page.WaitForAssertion(() =>
            {
                Assert.Empty(page.FindComponents<SkeletonDesignCard>());
                Assert.Empty(page.FindComponents<DesignCard>());
                Assert.Empty(page.FindComponents<MudAlert>());
            });
        }

        [Fact]
        public async Task WhenServiceRequestReturnsValuesThenCardsAreDisplayed()
        {
            // Arrange
            var results = CreatePage(1, totalItems: 4, pageSize: 12);
            var designService = new Mock<IDesignService>(MockBehavior.Strict);
            designService.Setup(x => x.GetPaginatedDesigns(1)).ReturnsAsync(results);
            await using var context = CreateContext(designService);

            // Act
            var page = context.RenderWithProviders<DesignsList>();

            // Assert
            Assert.Empty(page.FindComponents<SkeletonDesignCard>());
            Assert.Empty(page.FindComponents<MudPagination>());
            AssertCards(page, results.Designs);
        }

        [Fact]
        public async Task WhenServiceCallFailsErrorAlertIsDisplayed()
        {
            // Arrange
            var designService = new Mock<IDesignService>(MockBehavior.Strict);
            designService.Setup(x => x.GetPaginatedDesigns(1))
                .ThrowsAsync(new InvalidOperationException("Test error"));
            await using var context = CreateContext(designService);

            // Act
            var page = context.RenderWithProviders<DesignsList>();

            // Assert
            AssertLoadError(page);
        }

        [Fact]
        public async Task WhenServiceReturnsEmptyThenNothingIsDisplayed()
        {
            // Arrange
            var designService = new Mock<IDesignService>(MockBehavior.Strict);
            designService.Setup(x => x.GetPaginatedDesigns(1))
                .ReturnsAsync(CreatePage(1, totalItems: 0));
            await using var context = CreateContext(designService);

            // Act
            var page = context.RenderWithProviders<DesignsList>();

            // Assert
            Assert.Empty(page.FindComponents<SkeletonDesignCard>());
            Assert.Empty(page.FindComponents<DesignCard>());
            Assert.Empty(page.FindComponents<MudAlert>());
            Assert.Empty(page.FindComponents<MudPagination>());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task WhenPageIsSelectedThenLoadingIsFollowedByResultsOrError(bool requestFails)
        {
            // Arrange
            var firstPage = CreatePage(1);
            var secondPage = CreatePage(2);
            var pending = CreatePendingRequest();
            var designService = new Mock<IDesignService>(MockBehavior.Strict);
            designService.Setup(x => x.GetPaginatedDesigns(1)).ReturnsAsync(firstPage);
            designService.Setup(x => x.GetPaginatedDesigns(2)).Returns(pending.Task);
            await using var context = CreateContext(designService);
            var page = context.RenderWithProviders<DesignsList>();
            var pagination = page.FindComponent<MudPagination>();
            Assert.Equal(2, pagination.Instance.GetState(x => x.Count));
            Assert.Equal(1, pagination.Instance.GetState(x => x.Selected));
            Assert.False(pagination.Instance.Disabled);
            AssertCards(page, firstPage.Designs);

            // Act: keep the callback pending so the intermediate loading state is observable.
            var selection = page.InvokeAsync(() => pagination.Instance.SelectedChanged.InvokeAsync(2));
            page.WaitForAssertion(() => AssertLoading(page));

            if (requestFails)
                pending.SetException(new InvalidOperationException("Page request failed"));
            else
                pending.SetResult(secondPage);
            await selection;

            // Assert
            page.WaitForAssertion(() =>
            {
                if (requestFails)
                {
                    AssertLoadError(page);
                }
                else
                {
                    Assert.Empty(page.FindComponents<SkeletonDesignCard>());
                    Assert.Empty(page.FindComponents<MudAlert>());
                    AssertCards(page, secondPage.Designs);
                    Assert.Equal(2, page.FindComponent<MudPagination>().Instance.GetState(x => x.Selected));
                    Assert.Equal(2, page.FindComponent<MudPagination>().Instance.GetState(x => x.Count));
                }
            });
            designService.Verify(x => x.GetPaginatedDesigns(1), Times.Once);
            designService.Verify(x => x.GetPaginatedDesigns(2), Times.Once);
            designService.VerifyNoOtherCalls();
        }

        // Each test owns and asynchronously disposes its context; no state is shared between tests.
        private static ComponentTestContext CreateContext(Mock<IDesignService> designService)
        {
            var context = new ComponentTestContext();
            context.Services.AddSingleton(designService.Object);
            context.Services.AddSingleton(Mock.Of<IImageService>());
            return context;
        }

        private static TaskCompletionSource<PaginatedDesignResults> CreatePendingRequest() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static PaginatedDesignResults CreatePage(int pageNumber, int totalItems = 3, int pageSize = 2)
        {
            var count = Math.Clamp(totalItems - (pageNumber - 1) * pageSize, 0, pageSize);
            return new PaginatedDesignResults
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = totalItems,
                Designs = Enumerable.Range(0, count).Select(i => new DesignModel
                {
                    Id = Guid.NewGuid(),
                    Name = $"Page {pageNumber} design {i}",
                    ImageName = $"page-{pageNumber}-{i}.webp",
                    IsAvailable = true
                }).ToList()
            };
        }

        private static void AssertCards(IRenderedComponent<DesignsList> page, IEnumerable<DesignModel> expected)
        {
            Assert.Equal(expected.Select(x => x.Id),
                page.FindComponents<DesignCard>().Select(x => x.Instance.Design.Id));
        }

        private static void AssertLoading(IRenderedComponent<DesignsList> page)
        {
            Assert.Equal(4, page.FindComponents<SkeletonDesignCard>().Count);
            Assert.Empty(page.FindComponents<DesignCard>());
            Assert.Empty(page.FindComponents<MudPagination>());
            Assert.Empty(page.FindComponents<MudAlert>());
        }

        private static void AssertLoadError(IRenderedComponent<DesignsList> page)
        {
            Assert.Empty(page.FindComponents<SkeletonDesignCard>());
            Assert.Empty(page.FindComponents<DesignCard>());
            Assert.Empty(page.FindComponents<MudPagination>());
            var alert = page.FindComponent<MudAlert>();
            Assert.Equal(Severity.Error, alert.Instance.Severity);
            Assert.Contains("There has been an error retrieving available designs please try again later.", alert.Markup);
        }
    }
}
