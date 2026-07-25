using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260724173230_AlignManifestRoutesAndDocuments")]
public partial class AlignManifestRoutesAndDocuments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE service_routes
                DROP CONSTRAINT "FK_service_routes_branches_DestinationBranchId";

            DROP INDEX "IX_service_routes_OriginBranchId";

            ALTER TABLE shipping_price_catalogue
                ADD "ServiceRouteId" uuid;

            ALTER TABLE service_routes
                ALTER COLUMN "DestinationBranchId" DROP NOT NULL,
                ADD "DestinationCity" character varying(160) NOT NULL DEFAULT '',
                ADD "DestinationCountryCode" character varying(5) NOT NULL DEFAULT '',
                ADD "DestinationRegion" character varying(160),
                ADD "EstimatedTimeline" character varying(300) NOT NULL DEFAULT '',
                ADD "RowVersion" bigint NOT NULL DEFAULT 0;

            ALTER TABLE operational_manifests
                ADD "BillOfLadingNumber" character varying(120),
                ADD "FlightNumber" character varying(120),
                ADD "Notes" character varying(2000),
                ADD "VesselName" character varying(200),
                ADD "VoyageNumber" character varying(120);

            UPDATE service_routes AS route
            SET "DestinationCountryCode" = destination."CountryCode",
                "DestinationCity" = CASE destination."Code"
                    WHEN 'UK-MAN' THEN 'Manchester'
                    WHEN 'NG-LOS' THEN 'Lagos'
                    WHEN 'CN-GZ' THEN 'Guangzhou'
                    ELSE destination."Name"
                END,
                "EstimatedTimeline" = route."TransitDaysMin"::text || '–' ||
                    route."TransitDaysMax"::text || ' days',
                "RowVersion" = 1
            FROM branches AS destination
            WHERE destination."Id" = route."DestinationBranchId";

            INSERT INTO service_routes
                ("Id", "Code", "Name", "OriginBranchId", "DestinationBranchId",
                 "DestinationCountryCode", "DestinationCity", "DestinationRegion",
                 "EstimatedTimeline", "Mode", "TransitDaysMin", "TransitDaysMax",
                 "WeeklyDepartureDay", "VolumetricDivisor", "IsActive", "RowVersion",
                 "CreatedAt", "LastModifiedAt", "CreatedBy", "LastModifiedBy")
            SELECT DISTINCT ON (price."BranchId", price."ServiceCode", price."Method")
                price."Id",
                left(price."ServiceCode", 50),
                left(CASE WHEN btrim(price."ServiceArea") = '' THEN price."Name"
                    ELSE price."ServiceArea" END, 160),
                price."BranchId",
                destination."Id",
                left(split_part(price."DestinationCountryCodes", ',', 1), 5),
                left(CASE WHEN btrim(price."DestinationZone") = '' THEN price."ServiceArea"
                    ELSE price."DestinationZone" END, 160),
                NULL,
                left(COALESCE(NULLIF(price."EstimatedDelivery", ''),
                    'Timeline confirmed by operations'), 300),
                price."Method",
                1,
                1,
                NULL,
                CASE WHEN price."Method" = 'Air' THEN 5000 ELSE 1000000 END,
                bool_or(price."State" = 'Active') OVER (
                    PARTITION BY price."BranchId", price."ServiceCode", price."Method"),
                1,
                min(price."CreatedAt") OVER (
                    PARTITION BY price."BranchId", price."ServiceCode", price."Method"),
                NULL,
                NULL,
                NULL
            FROM shipping_price_catalogue AS price
            LEFT JOIN branches AS destination
                ON destination."CountryCode" =
                    split_part(price."DestinationCountryCodes", ',', 1)
               AND (
                    (destination."Code" = 'UK-MAN' AND
                        upper(price."DestinationZone") LIKE '%MANCHESTER%') OR
                    (destination."Code" = 'NG-LOS' AND
                        upper(price."DestinationZone") LIKE '%LAGOS%') OR
                    (destination."Code" = 'CN-GZ' AND
                        upper(price."DestinationZone") LIKE '%GUANGZHOU%')
               )
            WHERE price."Method" IN ('Air', 'Sea')
              AND btrim(price."ServiceCode") <> ''
              AND NOT EXISTS (
                  SELECT 1 FROM service_routes AS existing
                  WHERE existing."Code" = left(price."ServiceCode", 50)
              )
            ORDER BY price."BranchId", price."ServiceCode", price."Method",
                price."DisplayOrder", price."CreatedAt", price."Id";

            UPDATE shipping_price_catalogue AS price
            SET "ServiceRouteId" = route."Id"
            FROM service_routes AS route
            WHERE route."OriginBranchId" = price."BranchId"
              AND route."Code" = left(price."ServiceCode", 50)
              AND route."Mode" = price."Method"
              AND price."Method" IN ('Air', 'Sea');

            CREATE INDEX "IX_shipping_price_catalogue_ServiceRouteId"
                ON shipping_price_catalogue ("ServiceRouteId");

            CREATE INDEX "IX_service_routes_OriginBranchId_DestinationCountryCode_Destin~"
                ON service_routes (
                    "OriginBranchId", "DestinationCountryCode", "DestinationCity",
                    "Mode", "IsActive");

            ALTER TABLE service_routes
                ADD CONSTRAINT "FK_service_routes_branches_DestinationBranchId"
                FOREIGN KEY ("DestinationBranchId") REFERENCES branches ("Id")
                ON DELETE SET NULL;

            ALTER TABLE shipping_price_catalogue
                ADD CONSTRAINT "FK_shipping_price_catalogue_service_routes_ServiceRouteId"
                FOREIGN KEY ("ServiceRouteId") REFERENCES service_routes ("Id")
                ON DELETE SET NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE service_routes
                DROP CONSTRAINT "FK_service_routes_branches_DestinationBranchId";

            ALTER TABLE shipping_price_catalogue
                DROP CONSTRAINT "FK_shipping_price_catalogue_service_routes_ServiceRouteId";

            DROP INDEX "IX_shipping_price_catalogue_ServiceRouteId";
            DROP INDEX "IX_service_routes_OriginBranchId_DestinationCountryCode_Destin~";

            ALTER TABLE shipping_price_catalogue
                DROP COLUMN "ServiceRouteId";

            ALTER TABLE service_routes
                DROP COLUMN "DestinationCity",
                DROP COLUMN "DestinationCountryCode",
                DROP COLUMN "DestinationRegion",
                DROP COLUMN "EstimatedTimeline",
                DROP COLUMN "RowVersion";

            ALTER TABLE operational_manifests
                DROP COLUMN "BillOfLadingNumber",
                DROP COLUMN "FlightNumber",
                DROP COLUMN "Notes",
                DROP COLUMN "VesselName",
                DROP COLUMN "VoyageNumber";

            DELETE FROM service_routes
            WHERE "DestinationBranchId" IS NULL;

            UPDATE service_routes
            SET "DestinationBranchId" = '00000000-0000-0000-0000-000000000000'
            WHERE "DestinationBranchId" IS NULL;

            ALTER TABLE service_routes
                ALTER COLUMN "DestinationBranchId" SET NOT NULL,
                ALTER COLUMN "DestinationBranchId"
                    SET DEFAULT '00000000-0000-0000-0000-000000000000';

            CREATE INDEX "IX_service_routes_OriginBranchId"
                ON service_routes ("OriginBranchId");

            ALTER TABLE service_routes
                ADD CONSTRAINT "FK_service_routes_branches_DestinationBranchId"
                FOREIGN KEY ("DestinationBranchId") REFERENCES branches ("Id")
                ON DELETE RESTRICT;
            """);
    }
}
