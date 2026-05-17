using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePlansWithIndianTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replace the USD free/starter/pro/enterprise tiers with the INR three-tier
            // offering (annual / biennial / unlimited). All three tiers are uncapped on
            // sheets/users/floors — customers pick on commitment length, not features.
            //
            // Sequence matters:
            //   1. Insert the new plans.
            //   2. Repoint every existing Subscription off the old plans onto 'annual'
            //      so the FK delete in step 3 won't violate the Restrict rule.
            //   3. Delete the old plans.
            //
            // Idempotent: rerunning this on a DB where the new plans already exist is a
            // no-op for step 1 (codes are unique), a no-op for step 2 (no rows match),
            // and a no-op for step 3 (no old plans left).
            migrationBuilder.Sql(@"
DECLARE @annualId    UNIQUEIDENTIFIER;
DECLARE @biennialId  UNIQUEIDENTIFIER;
DECLARE @unlimitedId UNIQUEIDENTIFIER;

SELECT @annualId    = Id FROM Plans WHERE Code = 'annual';
SELECT @biennialId  = Id FROM Plans WHERE Code = 'biennial';
SELECT @unlimitedId = Id FROM Plans WHERE Code = 'unlimited';

IF @annualId IS NULL
BEGIN
    SET @annualId = NEWID();
    INSERT INTO Plans (Id, Code, Name, Description, MonthlyPriceCents, Currency,
                       MaxSheets, MaxUsers, MaxShopfloors, RetentionDays, SortOrder,
                       IsActive, TrialDays, BillingIntervalMonths, IsDefaultOnSignup,
                       CreatedAtUtc)
    VALUES (@annualId, 'annual', 'Annual',
            '1-year commitment. INR 4,00,000 billed up front.',
            3333333, 'INR',
            2147483647, 2147483647, 2147483647, -1, 10,
            1, 0, 12, 1,
            SYSUTCDATETIME());
END

IF @biennialId IS NULL
BEGIN
    SET @biennialId = NEWID();
    INSERT INTO Plans (Id, Code, Name, Description, MonthlyPriceCents, Currency,
                       MaxSheets, MaxUsers, MaxShopfloors, RetentionDays, SortOrder,
                       IsActive, TrialDays, BillingIntervalMonths, IsDefaultOnSignup,
                       CreatedAtUtc)
    VALUES (@biennialId, 'biennial', 'Biennial',
            '2-year commitment. INR 7,00,000 billed up front - saves INR 1,00,000 vs annual.',
            2916667, 'INR',
            2147483647, 2147483647, 2147483647, -1, 20,
            1, 0, 24, 0,
            SYSUTCDATETIME());
END

IF @unlimitedId IS NULL
BEGIN
    SET @unlimitedId = NEWID();
    INSERT INTO Plans (Id, Code, Name, Description, MonthlyPriceCents, Currency,
                       MaxSheets, MaxUsers, MaxShopfloors, RetentionDays, SortOrder,
                       IsActive, TrialDays, BillingIntervalMonths, IsDefaultOnSignup,
                       CreatedAtUtc)
    VALUES (@unlimitedId, 'unlimited', 'Unlimited',
            'Pay-as-you-go monthly. INR 20,000 / month, cancel any time.',
            2000000, 'INR',
            2147483647, 2147483647, 2147483647, -1, 30,
            1, 0, 1, 0,
            SYSUTCDATETIME());
END

-- Force exactly one IsDefaultOnSignup row (annual). Without this, an existing fresh
-- DB seeded under the old plans would have 'free' as default; ensure it lands on annual.
UPDATE Plans SET IsDefaultOnSignup = 0;
UPDATE Plans SET IsDefaultOnSignup = 1 WHERE Id = @annualId;

-- Repoint any subscription still referencing an old USD plan onto annual.
UPDATE Subscriptions
SET PlanId = @annualId
WHERE PlanId IN (SELECT Id FROM Plans WHERE Code IN ('free','starter','pro','enterprise'));

-- Now safe to drop the old plans.
DELETE FROM Plans WHERE Code IN ('free','starter','pro','enterprise');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No reversible Down — the original plan IDs are lost, and the catalog data
            // doesn't round-trip cleanly. If a rollback is needed, restore from backup
            // and revert by removing migration history row + redeploying older code.
        }
    }
}
