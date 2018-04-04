using LibraryData;
using LibraryDev.Models.Catalog;
using LibraryServices;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace LibraryDev.Controllers
{
    public class CatalogController : Controller
    {
        private ILibraryAsset _assets;
        private ICheckout _checkouts;

        public CatalogController(ILibraryAsset assets, ICheckout checkouts)
        {
            _assets = assets;
            _checkouts = checkouts;
        }

        public IActionResult Index()
        {
            var assetModel = _assets.GetAll();

            var listingResult = assetModel
                .Select(result => new AssetIndexListingModel
                {
                    Id = result.Id,
                    ImageUrl = result.ImageUrl,
                    AuthorOrDirector = _assets.GetAuthorOrDirector(result.Id),
                    DeweyCallNumber = _assets.GetDeweyIndex(result.Id),
                    Title = result.Title,
                    Type = _assets.GetType(result.Id)
                });

            var model = new AssetIndexModel()
            {
                Assets = listingResult
            };

            return View(model);
        }

        public IActionResult Detail(int id)
        {
            var asset = _assets.GetById(id);

            var currentHolds = _checkouts.GetCurrentHolds(id)
                .Select(h => new AssetHoldModel
                {
                    HoldPlaced = _checkouts.GetCurrentHoldPlaced(h.Id),
                    PatronName = _checkouts.GetCurrentHoldPatron(h.Id)
                 });
     

            var model = new AssetDetailModel {
                AssetId = id,
                Title = asset.Title,
                Type = _assets.GetType(id),
                Year = asset.Year,
                Cost = asset.Cost,
                Status = asset.Status.Name,
                ImageUrl = asset.ImageUrl,
                AuthorOrDirector = _assets.GetAuthorOrDirector(id),
                CurrentLocation = _assets.GetCurrentLocation(id).Name,
                DeweyCallNumber = _assets.GetDeweyIndex(id),
                CheckoutHistory = _checkouts.GetCheckoutHistory(id),
                CurrentAssociatedLibraryCard = _assets.GetLibraryCardByAssetId(id),
                ISBN = _assets.GetIsbn(id),
                LatestCheckout = _checkouts.GetLatestCheckout(id),
                CurrentHolds = currentHolds,
                PatronName = _checkouts.GetCurrentPatron(id)
            };
            return View(model);
        }
    }
}
