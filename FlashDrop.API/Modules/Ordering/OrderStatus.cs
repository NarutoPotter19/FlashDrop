namespace FlashDrop.API.Modules.Ordering
{

    //  OrderStatus defines the order lifecycle states.
    //
    // EF Core stores enum values as their underlying int by default:
    //   Pending   → 0 (stored in database as integer 0)
    //   Confirmed → 1
    //   Failed    → 2
    //
    // Explicit int values (= 0, = 1, = 2) are best practice:
    //   - Prevents accidental renumbering if enum members are reordered
    //   - Makes database values predictable and stable across code changes
    //   - If you insert Cancelled between Confirmed and Failed later,
    //     existing database values stay correct

    public enum OrderStatus
    {


            Pending   = 0,
    Confirmed = 1,
    Failed    = 2

    }
}
