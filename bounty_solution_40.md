2. Code Patch / Fix

**File: `src/config/Settings.cs`**
```csharp
public class Settings
{
    // ... existing settings ...
    
    /// <summary>
    /// Determines if staff/guards will intervene when a Bounty Hunter attacks a Wanted Customer.
    /// Default is false (staff stay out).
    /// </summary>
    public bool StaffInterveneInBountyFights { get; set; } = false;
}
```

**File: `src/Systems/CustomerInteractionSystem.cs`**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Bathhouse.Core;
using Bathhouse.Entities;

namespace Bathhouse.Systems
{
    public class CustomerInteractionSystem : ISystem
    {
        private readonly Settings _settings;
        private readonly List<Customer> _customers;
        private readonly List<Staff> _staff;

        public CustomerInteractionSystem(Settings settings, List<Customer> customers, List<Staff> staff)
        {
            _settings = settings;
            _customers = customers;
            _staff = staff;
        }

        public void Update(GameState state)
        {
            // Only process if there are at least 2 customers
            if (_customers.Count < 2) return;

            // Find Bounty Hunters
            var hunters = _customers.Where(c => c.IsBountyHunter).ToList();
            
            // Find Wanted Customers (excluding the hunters themselves if they are also wanted, though typically distinct roles)
            var wantedCustomers = _customers.Where(c => c.HasBounty && !c.IsBountyHunter).ToList();

            foreach (var hunter in hunters)
            {
                // Find the closest wanted customer
                var target = wantedCustomers
                    .Where(w => !w.IsDead && !w.IsInCombat)
                    .OrderBy(w => Vector3.Distance(hunter.Position, w.Position))
                    .FirstOrDefault();

                if (target != null)
                {
                    InitiateBountyFight(hunter, target);
                }
            }
        }

        private void InitiateBountyFight(Customer hunter, Customer target)
        {
            // Prevent multiple fights involving the same entities
            if (hunter.IsInCombat || target.IsInCombat) return;

            // Mark as in combat
            hunter.IsInCombat = true;
            target.IsInCombat = true;

            // Squad-only fight: Only the hunter and the target participate
            // (No other customers join)
            
            // Check Staff Intervention Setting
            if (_settings.St