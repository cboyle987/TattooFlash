using TattooFlash.Models;

namespace TattooFlash.Services
{
    public class MockDesignService : IDesignService
    {
        public List<DesignModel> Designs { get; set; }

        public MockDesignService()
        {
            Designs = new List<DesignModel>
            {
                new DesignModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Bat",
                    Theme = "Ozzy",
                    Price = 250,
                    ImageUrl = "/samples/Bat-flash.jpeg",
                    IsAvailable= true
                },
                new DesignModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Cocktail",
                    Theme = "Ozzy",
                    Price = 125,
                    ImageUrl = "/samples/cocktail-flash.jpeg",
                    IsAvailable= true
                },
                new DesignModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Locket",
                    Theme = "Ozzy",
                    Price = 100,
                    ImageUrl = "/samples/Locket-flash.jpeg",
                    IsAvailable= true
                },
                new DesignModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Perfume",
                    Theme = "Ozzy",
                    Price = 175,
                    ImageUrl = "/samples/Perfume-Flash.jpeg",
                    IsAvailable= true    
                },
                new DesignModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Shoe",
                    Theme = "Ozzy",
                    Price = 175,
                    ImageUrl = "/samples/shoe-flash.jpeg",
                    IsAvailable= false
                },
                new DesignModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Coffee table guy",
                    Theme = "Ozzy",
                    Price = 150,
                    ImageUrl = "/samples/ozzy1.jpg",
                    IsAvailable= true
                },
                new DesignModel
                {
                    Id = Guid.NewGuid(),
                    Name = "Sleepy lil guy",
                    Theme = "Ozzy",
                    Price = 120,
                    ImageUrl = "/samples/ozzy2.jpg",
                    IsAvailable= false
                },
              };
        }

        public Task<DesignModel> CreateDesignAsync(DesignModel design)
        {
            design.Id = Guid.NewGuid();
            Designs.Add(design);
            return Task.FromResult(design);
        }

        public Task<bool> DeleteDesignAsync(Guid id)
        {
            var design = Designs.FirstOrDefault(d => d.Id == id);
            if (design == null)
                return Task.FromResult(false);

            Designs.Remove(design);
            return Task.FromResult(true);
        }

        public Task<List<DesignModel>> GetAllDesignsAsync()
        {
            return Task.FromResult(Designs);
        }

        public Task<DesignModel?> GetDesignByIdAsync(Guid id)
        {
            var design = Designs.FirstOrDefault(d => d.Id == id);
            return Task.FromResult(design);
        }

        public Task<DesignModel> UpdateDesignAsync(Guid id, DesignModel design)
        {
            var existingDesign = Designs.FirstOrDefault(d => d.Id == id);
            if(existingDesign == null)
                throw new KeyNotFoundException("Design not found");
            Designs.Remove(existingDesign);
            Designs.Add(design);
            return Task.FromResult(design);
        }

        public Task ToggleAvailabilityAsync(Guid id)
        {
            var design = Designs.FirstOrDefault(d => d.Id == id);
            if(design == null)
                throw new KeyNotFoundException("Design not found");
            design.IsAvailable = !design.IsAvailable;
            return Task.CompletedTask;
        }
    }
}
