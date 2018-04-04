using LibraryData;
using LibraryData.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibraryServices
{
    public class CheckoutService : ICheckout
    {
        private LibraryContext _context;

        public CheckoutService(LibraryContext context)
        {
            _context = context;
        } 

        public void Add(Checkout newCheckout)
        {
            _context.Add(newCheckout);
            _context.SaveChanges();
        }

        public Checkout Get(int id)
        {
            return _context.Checkouts.FirstOrDefault(p => p.Id == id);
        }

        public IEnumerable<Checkout> GetAll()
        {
            return _context.Checkouts;
        }

        public void CheckOutItem(int assetId, int libraryCardId)
        {
            if (IsCheckedOut(assetId))
            {
                return;
                // Add logic here to handle feedback to the user.
            }

            var item = _context.LibraryAssets
                .Include(a => a.Status)
                .FirstOrDefault(a => a.Id == assetId);

            _context.Update(item);

            item.Status = _context.Status
                .FirstOrDefault(a => a.Name == "Checked Out");

            var now = DateTime.Now;

            var libraryCard = _context.LibraryCard
                .Include(c => c.Checkouts)
                .FirstOrDefault(a => a.Id == libraryCardId);

            var checkout = new Checkout
            {
                LibraryAsset = item,
                LibraryCard = libraryCard,
                Since = now,
                Until = GetDefaultCheckoutTime(now)
            };

            _context.Add(checkout);

            var checkoutHistory = new CheckoutHistory
            {
                CheckedOut = now,
                LibraryAsset = item,
                LibraryCard = libraryCard
            };

            _context.Add(checkoutHistory);
            _context.SaveChanges();
        }

        //public void MarkLost(int assetId)
        //{
        //    var item = _context.LibraryAssets
        //        .FirstOrDefault(a => a.Id == assetId);

        //    _context.Update(item);

        //    item.Status = _context.Status.FirstOrDefault(a => a.Name == "Lost");

        //    _context.SaveChanges();
        //}

        public void MarkLost(int assetId)
        {
            UpdateAssetStatus(assetId, "Lost");
            _context.SaveChanges();
        }

        public void MarkFound(int assetId)
        {
            var now = DateTime.Now;

            UpdateAssetStatus(assetId, "Available");


            RemoveExistingCheckouts(assetId);
            CloseExistingCheckoutHistory(assetId, now);

            var checkout = _context.Checkouts
                .FirstOrDefault(co => co.LibraryAsset.Id == assetId);

            if (checkout != null)
            {
                _context.Remove(checkout);
            }

            var history = _context.CheckoutHistory
                .FirstOrDefault(h => h.LibraryAsset.Id == assetId
                && h.CheckedIn == null);


            if (history != null)
            {
                _context.Update(history);
                history.CheckedIn = now;
            }
        }

        public void PlaceHold(int assetId, int libraryCardId)
        {
            var now = DateTime.Now;
            var asset = _context.LibraryAssets
                .FirstOrDefault(a => a.Id == assetId);
            var card = _context.LibraryCard
                .FirstOrDefault(c => c.Id == libraryCardId);

            if (asset.Status.Name == "Available")
            {
                UpdateAssetStatus(assetId, "On Hold");
            }

            var hold = new Hold
            {
                HoldPlaced = now,
                LibraryAsset = asset,
                LibraryCard = card
            };
            _context.Add(hold);
            _context.SaveChanges();
        }


        public void CheckInItem(int assetId, int libraryCard)
        {
            var now = DateTime.Now;

            var item = _context.LibraryAssets
                .FirstOrDefault(a => a.Id == assetId);

            _context.Update(item);

            // remove any existing checkouts on the item
            var checkout = _context.Checkouts
                .Include(c => c.LibraryAsset)
                .Include(c => c.LibraryCard)
                .FirstOrDefault(a => a.LibraryAsset.Id == assetId);
            if (checkout != null)
            {
                _context.Remove(checkout);
            }

            // close any existing checkout history
            var history = _context.CheckoutHistory
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .FirstOrDefault(h =>
                h.LibraryAsset.Id == assetId
                && h.CheckedIn == null);
            if (history != null)
            {
                _context.Update(history);
                history.CheckedIn = now;
            }

            // look for current holds
            var currentHolds = _context.Holds
                .Include(a => a.LibraryAsset)
                .Include(a => a.LibraryCard)
                .Where(a => a.LibraryAsset.Id == assetId);

            // if there are current holds, check out the item to the earliest
            if (currentHolds.Any())
            {
                CheckoutToEarliestHold(assetId, currentHolds);
                return;
            }

            // otherwise, set item status to available
            item.Status = _context.Status
                .FirstOrDefault(a => a.Name == "Available");

            _context.SaveChanges();
        }

        private void CheckoutToEarliestHold(int assetId, IQueryable<Hold> currentHolds)
        {
            var earliestHold = currentHolds.OrderBy(holds =>
            holds.HoldPlaced).FirstOrDefault();

            var card = earliestHold.LibraryCard;

            _context.Remove(earliestHold);
            _context.SaveChanges();

            CheckoutItem(assetId, card.Id);
        }

        public IEnumerable<CheckoutHistory> GetCheckoutHistory(int id)
        {
            return _context.CheckoutHistory
                .Include(h => h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .Where(h => h.LibraryAsset.Id == id);
        }

        public Checkout GetLatestCheckout(int assetId)
        {
            return _context.Checkouts
                .Where(c => c.LibraryAsset.Id == assetId)
                .OrderByDescending(c => c.Since)
                .FirstOrDefault();
        }


        public int GetAvailableCopies(int id)
        {
            var numberOfCopies = GetNumberOfCopies(id);

            var numberCheckedOut = _context.Checkouts
                .Where(a => a.LibraryAsset.Id == id
                         && a.LibraryAsset.Status.Name == "Checked Out")
                         .Count();

            return numberOfCopies - numberCheckedOut;
        }

        public int GetNumberOfCopies(int id)
        {
            return _context.LibraryAssets
                .FirstOrDefault(a => a.Id == id)
                .NumberOfCopies;
        }

        private DateTime GetDefaultCheckoutTime(DateTime now)
        {
            return now.AddDays(30);
        }

        public bool IsCheckedOut(int id)
        {
            return _context.Checkouts
                 .Where(co => co.LibraryAsset.Id == id).Any();
        }

        public string GetCurrentHoldPatronName(int holdId)
        {
            var hold = _context.Holds
                 .Include(h => h.LibraryAsset)
                 .Include(h => h.LibraryCard)
                 .FirstOrDefault(h => h.Id == holdId);

            var cardID = hold?.LibraryCard.Id;

            var patron = _context.Patrons
                .Include(p => p.LibraryCard)
                .FirstOrDefault(p => p.LibraryCard.Id == cardID);

            return patron?.FirstName + " " + patron?.LastName;
        }

        public DateTime GetCurrentHoldPlaced(int holdId)
        {
            return _context.Holds
                  .Include(h => h.LibraryAsset)
                  .Include(h => h.LibraryCard)
                  .FirstOrDefault(h => h.Id == holdId)
                  .HoldPlaced;
        }

        public IEnumerable<Hold> GetCurrentHolds(int id)
        {
            return _context.Holds
                .Include(h => h.LibraryAsset)
                .Where(h => h.LibraryAsset.Id == id);
        }


        public string GetCurrentPatron(int id)
        {
            var checkout = _context.Checkouts
               .Include(a => a.LibraryAsset)
               .Include(a => a.LibraryCard)
               .Where(a => a.LibraryAsset.Id == id)
               .FirstOrDefault();

            if (checkout == null)
            {
                return "Not checked out.";
            }

            var cardId = checkout.LibraryCard.Id;

            var patron = _context.Patrons
                .Include(p => p.LibraryCard)
                .Where(c => c.LibraryCard.Id == cardId)
                .FirstOrDefault();

            return patron.FirstName + " " + patron.LastName;
        }

        //to split in futture

        //public string GetCurrentCheckoutPatron(int assetId)
        //{
        //    var checkout = GetCheckoutByAssetId(assetId);
        //    if (checkout == null)
        //    {
        //        return "Not Checked out.";
        //    };

        //    var cardId = checkout.Patrons;

        //    var patron = _context.Patrons
        //        .Include(p => p.LibraryCard)
        //        .FirstOrDefault(p => p.LibraryCard.Id == cardId);

        //    return patron.FirstName + " " + patron.LastName;
        //}

        private object GetCheckoutByAssetId(int assetId)
        {
            return _context.Checkouts
                 .Include(co => co.LibraryAsset)
                 .Include(co => co.LibraryCard)
                 .FirstOrDefault(co => co.LibraryAsset.Id == assetId);
        }

        public Checkout GetById(int checkoutId)
        {
            return GetAll().FirstOrDefault(checkout =>
            checkout.Id == checkoutId);
        }

     

   
        private void UpdateAssetStatus(int assetId, string v)
        {
            var item = _context.LibraryAssets
               .FirstOrDefault(a => a.Id == assetId);
            _context.Update(item);

            item.Status = _context.Status.FirstOrDefault(status =>
            status.Name == "Available");
        }

        private void CloseExistingCheckoutHistory(int assetId, DateTime now)
        {
            var history = _context.CheckoutHistory
               .FirstOrDefault(h => h.LibraryAsset.Id == assetId
               && h.CheckedIn == null);


            if (history != null)
            {
                _context.Update(history);
                history.CheckedIn = now;
            }
        }

        private void RemoveExistingCheckouts(int assetId)
        {
            var checkout = _context.Checkouts
               .FirstOrDefault(co => co.LibraryAsset.Id == assetId);

            if (checkout != null)
            {
                _context.Remove(checkout);
            }
        }

        private void RemoveExistingCheckouts(int v, int assetId)
        {
            throw new NotImplementedException();
        }

 

     



        public void CheckoutItem(int assetId, int libraryCardId)
        {
            if (IsCheckedOut(assetId))
            {
                return;
                //Display message the item is already checkout
            }

            var item = _context.LibraryAssets
                .FirstOrDefault(a => a.Id == assetId);

            UpdateAssetStatus(assetId, "Checked Out");
            var libraryCard = _context.LibraryCard
                .Include(card => card.Checkouts)
                .FirstOrDefault(card => card.Id == libraryCardId);

            var now = DateTime.Now;
            var checkout = new Checkout
            {
                LibraryAsset = item,
                LibraryCard = libraryCard,
                Since = now,
                Until = GetDefaultCheckoutTime(now)
            };
            _context.Add(checkout);

            var checkoutHistory = new CheckoutHistory
            {
                CheckedOut = now,
                LibraryCard = libraryCard,
                LibraryAsset = item
            };
            _context.Add(checkoutHistory);
            _context.SaveChanges();
        }


        public void CheckInItem(int assetId)
        {
            var now = DateTime.Now;
            var item = _context.LibraryAssets
         .FirstOrDefault(a => a.Id == assetId);

            // remove any existing checkouts on the item
            RemoveExistingCheckouts(assetId);
            //close any existing checkouts history
            CloseExistingCheckoutHistory(assetId, now);

            var currentHolds = _context.Holds
                .Include(h=> h.LibraryAsset)
                .Include(h => h.LibraryCard)
                .Where(h => h.LibraryAsset.Id == assetId);

            if (currentHolds.Any())
            {
                CheckoutToEarliestHold(assetId,currentHolds);
            }

            UpdateAssetStatus(assetId, "Available");

            _context.SaveChanges();
        }

  

        private void CheckoutToEarliestHold(int assetId)
        {
            throw new NotImplementedException();
        }







        public string GetCurrentHoldPatron(int id)
        {
            throw new NotImplementedException();
        }

        string ICheckout.GetCurrentHoldPlaced(int holdId)
        {
            var hold = _context.Holds
              .Include(a => a.LibraryAsset)
              .Include(a => a.LibraryCard)
              .Where(v => v.Id == holdId);

            return hold.Select(a => a.HoldPlaced)
                .FirstOrDefault().ToString();
        }

    }
}
