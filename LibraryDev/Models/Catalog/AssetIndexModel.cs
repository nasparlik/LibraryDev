using System.Collections.Generic;

namespace LibraryDev.Models.Catalog
{
    public class AssetIndexModel
    {
        public IEnumerable<AssetIndexListingModel> Assets { get; set; }
    }
}
