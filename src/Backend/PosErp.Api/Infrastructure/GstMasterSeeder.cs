using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PosErp.Api.Infrastructure;

/// <summary>
/// Seeds the official Indian GST Slab master and GstHsnMasterIndia reference table.
/// Source: CBIC Notifications 1/2017-Central Tax (Rate) & 2/2017-Central Tax (Rate)
/// and all subsequent amendments up to Finance Act 2024 / GST Council 53rd Meeting (Jun 2024).
/// Reference: https://cbic-gst.gov.in/gst-goods-services-rates.html
/// </summary>
public static class GstMasterSeeder
{
    // Fixed Tax Slab GUIDs — idempotent across all restarts
    public const string SLAB_0    = "10000000-0000-0000-0000-000000000001";
    public const string SLAB_5    = "10000000-0000-0000-0000-000000000002";
    public const string SLAB_12   = "10000000-0000-0000-0000-000000000003";
    public const string SLAB_18   = "10000000-0000-0000-0000-000000000004";
    public const string SLAB_28   = "10000000-0000-0000-0000-000000000005";
    public const string SLAB_28C  = "10000000-0000-0000-0000-000000000006"; // 28% + 12% Cess

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // ── STEP 1: Create GstHsnMasterIndia table ───────────────────────────
        await context.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS gst_hsn_master_india (
                id              UUID         PRIMARY KEY,
                hsn_code         VARCHAR(12)  NOT NULL,
                description     TEXT         NOT NULL,
                category        VARCHAR(60)  NOT NULL DEFAULT '',
                example_products TEXT         NOT NULL DEFAULT '',
                gst_rate_percent  NUMERIC(5,2) NOT NULL DEFAULT 0,
                cgst_rate        NUMERIC(5,2) NOT NULL DEFAULT 0,
                sgst_rate        NUMERIC(5,2) NOT NULL DEFAULT 0,
                igst_rate        NUMERIC(5,2) NOT NULL DEFAULT 0,
                cess_rate        NUMERIC(5,2) NOT NULL DEFAULT 0,
                is_exempt        BOOLEAN      NOT NULL DEFAULT FALSE,
                notes           TEXT,
                notification_ref VARCHAR(120),
                tax_slab_id       UUID,
                created_at       TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                is_deleted       BOOLEAN NOT NULL DEFAULT FALSE
            );
            CREATE INDEX IF NOT EXISTS idx_gsthsn_hsncode ON gst_hsn_master_india(hsn_code);
            CREATE INDEX IF NOT EXISTS idx_gsthsn_category ON gst_hsn_master_india(category);
        ");

        // ── STEP 2: Upsert the 6 standard Indian GST TaxSlab records ─────────
        var slabs = new[]
        {
            (SLAB_0,   "GST 0% — Exempt / Nil-rated",     0m,    0m,    0m,   0m),
            (SLAB_5,   "GST 5%",                           2.5m,  2.5m,  5m,   0m),
            (SLAB_12,  "GST 12%",                          6m,    6m,    12m,  0m),
            (SLAB_18,  "GST 18%",                          9m,    9m,    18m,  0m),
            (SLAB_28,  "GST 28%",                          14m,   14m,   28m,  0m),
            (SLAB_28C, "GST 28% + Cess (Demerit Goods)",   14m,   14m,   28m,  12m),
        };

        foreach (var (id, name, cgst, sgst, igst, cess) in slabs)
        {
            await context.Database.ExecuteSqlRawAsync($@"
                INSERT INTO tax_slabs (id, name, cgst_rate, sgst_rate, igst_rate, cess_rate, created_at, is_deleted)
                VALUES ('{id}','{name}',{cgst},{sgst},{igst},{cess},NOW(),false)
                ON CONFLICT (id) DO UPDATE
                    SET name=EXCLUDED.name,
                        cgst_rate=EXCLUDED.cgst_rate,
                        sgst_rate=EXCLUDED.sgst_rate,
                        igst_rate=EXCLUDED.igst_rate,
                        cess_rate=EXCLUDED.cess_rate;
            ");
        }
        Console.WriteLine("[GST] TaxSlab master: 0%, 5%, 12%, 18%, 28%, 28%+Cess seeded.");

        // ── STEP 3: Seed GstHsnMasterIndia (only once, idempotent) ───────────
        var alreadySeeded = await context.GstHsnMaster.AnyAsync();
        if (alreadySeeded)
        {
            Console.WriteLine("[GST] GstHsnMasterIndia already seeded — skipping.");
            return;
        }

        // Record layout: (HsnCode, Description, Category, ExampleProducts, GstRate%, SlabId, IsExempt, Notes, NotifRef)
        var entries = new List<(string hsn, string desc, string cat, string examples, decimal rate, string slab, bool exempt, string notes, string notif)>
        {
            // ══════════════════════════════════════════════════════════
            // 0% GST — EXEMPT / NIL-RATED
            // Notification 2/2017-Central Tax (Rate)
            // ══════════════════════════════════════════════════════════
            ("0401",    "Fresh milk, pasteurised milk (not UHT, not sweetened)",
                        "DAIRY",       "Fresh milk, toned milk, double-toned milk",
                        0, SLAB_0, true,
                        "Branded packaged fresh milk 0%; UHT/flavored milk 5%",
                        "Notif 2/2017-CT(R) Sl.1"),

            ("0407",    "Birds eggs (fresh, in shell)",
                        "DAIRY",       "Hen eggs, duck eggs, table eggs",
                        0, SLAB_0, true,
                        "Fresh shell eggs - exempt; processed egg products may attract GST",
                        "Notif 2/2017-CT(R) Sl.19"),

            ("0701",    "Fresh or chilled vegetables (unprocessed)",
                        "VEGETABLES",  "Onion, Tomato, Potato, Carrot, Beetroot, Brinjal, Cucumber",
                        0, SLAB_0, true,
                        "Only fresh/chilled vegetables; cut/packaged veg may attract 5%",
                        "Notif 2/2017-CT(R) Sl.12"),

            ("0801",    "Fresh coconuts, cashew nuts (raw), other fresh nuts",
                        "FRUITS",      "Fresh tender coconut, raw cashew",
                        0, SLAB_0, true,
                        "Fresh only; dried/roasted nuts attract 5%",
                        "Notif 2/2017-CT(R) Sl.24"),

            ("0809",    "Fresh fruits (apricots, cherries, peaches, plums, etc.)",
                        "FRUITS",      "Fresh apples, bananas, grapes, mangoes, oranges, guava",
                        0, SLAB_0, true,
                        "Fresh whole fruits only",
                        "Notif 2/2017-CT(R) Sl.29"),

            ("0901.11", "Unroasted, not decaffeinated coffee beans / coffee husks",
                        "BEVERAGE",    "Raw green coffee beans, coffee husk",
                        0, SLAB_0, true,
                        "Roasted/ground coffee 5%; instant coffee extracts 12%",
                        "Notif 2/2017-CT(R) Sl.30"),

            ("0902.10", "Green tea (not fermented, unprocessed leaves)",
                        "BEVERAGE",    "Raw green tea leaves",
                        0, SLAB_0, true,
                        "Packaged processed tea bags 5%",
                        "Notif 2/2017-CT(R) Sl.33"),

            ("1006.10", "Rice (not pre-packed, not branded, loose sale)",
                        "GROCERY",     "Loose rice sold in open bags",
                        0, SLAB_0, true,
                        "Branded/pre-packed rice attracts 5% w.e.f. 18-Jul-2022",
                        "Notif 2/2017-CT(R) Sl.57"),

            ("1001.10", "Wheat (not pre-packed, not branded, loose)",
                        "GROCERY",     "Loose wheat grains",
                        0, SLAB_0, true,
                        "Branded pre-packed wheat 5% (w.e.f. 18-Jul-2022)",
                        "Notif 2/2017-CT(R) Sl.52"),

            ("1101.00", "Wheat flour / atta / maida (unbranded, loose)",
                        "GROCERY",     "Loose atta/maida/sooji from local flour mill",
                        0, SLAB_0, true,
                        "Branded pre-packed atta (Aashirvaad, Pillsbury) 5%",
                        "Notif 2/2017-CT(R) Sl.59"),

            ("0713.10", "Dried peas / chickpeas (unbranded, loose)",
                        "GROCERY",     "Loose chana, kabuli chana",
                        0, SLAB_0, true,
                        "Pre-packed branded pulses 5% (w.e.f. 18-Jul-2022)",
                        "Notif 2/2017-CT(R) Sl.66"),

            ("2501.00", "Salt — all forms",
                        "GROCERY",     "Tata Salt, Annapurna iodized salt, rock salt, sea salt, pink Himalayan salt",
                        0, SLAB_0, true,
                        "ALL salt is EXEMPT regardless of brand or packaging",
                        "Notif 2/2017-CT(R) Sl.102"),

            ("9619.00", "Sanitary napkins / sanitary towels",
                        "PERSONAL_CARE","Whisper, Stayfree, Sofy, Carefree sanitary pads",
                        0, SLAB_0, true,
                        "Reduced from 12% to 0% at GST Council 28th Meeting, Jul-2018",
                        "Notif 19/2018-CT(R) Jul-2018"),

            ("4901",    "Printed books, newspapers, maps, journals",
                        "STATIONERY",  "Textbooks, novels, dictionaries, magazines, newspapers",
                        0, SLAB_0, true,
                        "",
                        "Notif 2/2017-CT(R) Sl.119"),

            ("0201",    "Fresh meat (not frozen/packaged)",
                        "PROTEIN",     "Fresh mutton, chicken, beef at butcher",
                        0, SLAB_0, true,
                        "Frozen/packaged meat attracts 5%",
                        "Notif 2/2017-CT(R) Sl.4"),

            ("0302",    "Fresh fish (not frozen, not packaged)",
                        "PROTEIN",     "Fresh fish at retail fish market",
                        0, SLAB_0, true,
                        "Frozen/packaged fish attracts 5%",
                        "Notif 2/2017-CT(R) Sl.8"),

            ("0409",    "Natural honey (unbranded)",
                        "GROCERY",     "Raw unbranded honey",
                        0, SLAB_0, true,
                        "Branded honey (Dabur, Zandu) attracts 5%",
                        "Notif 2/2017-CT(R) Sl.21"),

            ("2208",    "Alcohol for human consumption (Beer, Wine, Spirits)",
                        "ALCOHOL",     "Royal Stag, Old Monk, Kingfisher Beer — OUTSIDE GST",
                        0, SLAB_0, true,
                        "Alcohol for human consumption is NOT under GST — State VAT/Excise applies",
                        "Art.246A Indian Constitution"),

            // ══════════════════════════════════════════════════════════
            // 5% GST
            // Notification 1/2017-CT(R) Schedule-I
            // ══════════════════════════════════════════════════════════
            ("0402",    "Milk powder, condensed milk, skimmed milk powder, cream",
                        "DAIRY",       "Amul milk powder, Nestle Milkmaid condensed milk, Amul fresh cream",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.18"),

            ("0405",    "Butter (dairy)",
                        "DAIRY",       "Amul Butter, Britannia Butter, Mother Dairy butter",
                        5, SLAB_5, false,
                        "Note: Ghee/clarified butter is 12%",
                        "Notif 1/2017-CT(R) Sch-I Sl.21"),

            ("0409.00", "Natural honey (branded, packaged)",
                        "GROCERY",     "Dabur Honey, Zandu Pure Honey, Apis Himalayan honey",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.21"),

            ("0902.40", "Black tea / green tea (processed, packaged)",
                        "BEVERAGE",    "Tata Tea Gold, Red Label, Brooke Bond, Wagh Bakri, Lipton, Society Tea",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.35"),

            ("0901.21", "Roasted coffee, ground coffee (not instant)",
                        "BEVERAGE",    "Bru filter coffee, Nescafe ground coffee, Continental",
                        5, SLAB_5, false,
                        "Instant coffee/extracts attract 12%",
                        "Notif 1/2017-CT(R) Sch-I Sl.32"),

            ("1006.30", "Rice (branded, pre-packed, labelled)",
                        "GROCERY",     "India Gate Basmati, Kohinoor Basmati, Daawat, Fortune, Lakshmi rice",
                        5, SLAB_5, false,
                        "Applicable w.e.f. 18-Jul-2022 per 47th GST Council decision",
                        "GST Council 47th Meeting Jul-2022"),

            ("1001.99", "Wheat / Atta — branded, pre-packed, labelled",
                        "GROCERY",     "Aashirvaad Shudh Chakki Atta, Pillsbury, Nature Fresh, 24 Mantra organic atta",
                        5, SLAB_5, false,
                        "Applicable w.e.f. 18-Jul-2022; unbranded/loose atta 0%",
                        "GST Council 47th Meeting Jul-2022"),

            ("1101.10", "Wheat flour/maida/rawa — branded, pre-packed",
                        "GROCERY",     "Fortune Maida, MDH Besan, Rajdhani atta, Natureland organic",
                        5, SLAB_5, false,
                        "W.e.f. 18-Jul-2022",
                        "GST Council 47th Meeting Jul-2022"),

            ("0713.90", "Dried pulses — pre-packed, branded",
                        "GROCERY",     "Tata Sampann dal, MDH chana dal, 24 Mantra organic dal, Fortune pulses",
                        5, SLAB_5, false,
                        "W.e.f. 18-Jul-2022",
                        "GST Council 47th Meeting Jul-2022"),

            ("1507.90", "Refined soyabean oil",
                        "GROCERY",     "Fortune Soya oil, Dhara soya oil, Sundrop soyabean oil",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.75"),

            ("1511",    "Palm oil (refined)",
                        "GROCERY",     "Ruchi Gold palm oil, Freedom refined palm oil",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.77"),

            ("1512",    "Sunflower oil / Safflower oil",
                        "GROCERY",     "Saffola active oil, Sundrop sunflower oil, NatureFresh",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.78"),

            ("1513",    "Coconut oil (edible grade)",
                        "GROCERY",     "Parachute Edible Coconut Oil, Cocoguru, KLF Nirmal",
                        5, SLAB_5, false,
                        "Coconut oil sold as hair oil attracts 18% GST",
                        "Notif 1/2017-CT(R) Sch-I Sl.79"),

            ("1514",    "Mustard oil / Rapeseed oil",
                        "GROCERY",     "Dhara mustard oil, Engine mustard oil, Patanjali mustard oil",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.80"),

            ("1701",    "Sugar (white, refined, granulated, brown sugar)",
                        "GROCERY",     "Madhur refined sugar, Parrys sugar, Uttam sugar",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.91"),

            ("0203",    "Packaged / frozen meat (chicken, mutton)",
                        "PROTEIN",     "Suguna frozen chicken, Venky fresh frozen, ITC Master Chef",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I"),

            ("0303",    "Packaged / frozen fish",
                        "PROTEIN",     "Frozen fish fillets, frozen prawns, McCain fish fingers",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I"),

            ("2103",    "Sauces, ketchup, condiments, mustard preparations",
                        "GROCERY",     "Kissan tomato ketchup, Maggi rich tomato sauce, Lee & Perrins, French mustard",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.119"),

            ("2104",    "Soups, broths, food preparations for soups",
                        "GROCERY",     "Knorr tomato soup, Maggi soup sachets, Ching's instant soup",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.120"),

            ("2106.10", "Malted food drinks (cocoa < 50%) — Horlicks, Bournvita",
                        "BEVERAGE",    "Horlicks, Bournvita (regular), Complan, Boost, Milo, Ovaltine",
                        5, SLAB_5, false,
                        "If cocoa content > 50% of total, attracts 18%; most health drinks are 5%",
                        "Notif 1/2017-CT(R) Sch-I"),

            ("3004",    "Medicines / pharmaceutical formulations (branded OTC)",
                        "MEDICAL",     "Crocin, Paracetamol, Dettol antiseptic liquid, Eno, Gelusil",
                        5, SLAB_5, false,
                        "Life-saving drugs at 0%; regular OTC medicines 5%; some at 12%",
                        "Notif 1/2017-CT(R) Sch-I"),

            ("3005",    "Wadding, gauze, bandages — medical grade",
                        "MEDICAL",     "Dettol plasters, elastoplast bandage, First Aid gauze rolls",
                        5, SLAB_5, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-I Sl.255"),

            // ══════════════════════════════════════════════════════════
            // 12% GST
            // Notification 1/2017-CT(R) Schedule-II
            // ══════════════════════════════════════════════════════════
            ("0405.90", "Ghee / Clarified butter (branded, packaged)",
                        "DAIRY",       "Amul Pure Ghee, Mother Dairy Ghee, Gowardhan, Nestle Pure Ghee",
                        12, SLAB_12, false,
                        "Butter is 5%; ghee is 12%",
                        "Notif 1/2017-CT(R) Sch-II Sl.17"),

            ("0406",    "Cheese (all varieties)",
                        "DAIRY",       "Amul Processed Cheese Slices, Britannia Cheese, La Vache qui rit, Amul Emmental",
                        12, SLAB_12, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-II Sl.14"),

            ("2001",    "Vegetables, fruits, nuts preserved by vinegar (pickles)",
                        "GROCERY",     "Mixed pickle, mango pickle, lemon pickle, Priya pickle, Mothers Recipe",
                        12, SLAB_12, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-II Sl.77"),

            ("2008",    "Fruits / nuts / edible plant parts in sugar or syrup; jams",
                        "GROCERY",     "Kissan jam, Mapro jam, tutti-frutti, glazed fruits, marmalade",
                        12, SLAB_12, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-II Sl.86"),

            ("2009",    "Fruit/vegetable juices (100%, unsweetened)",
                        "BEVERAGE",    "Real fruit juice 100%, Tropicana Pure juice, Patanjali juice",
                        12, SLAB_12, false,
                        "Sweetened fruit beverages/nectars 12%; aerated fruit drinks 28%+Cess",
                        "Notif 1/2017-CT(R) Sch-II"),

            ("2101",    "Coffee extracts, essences, instant coffee, chicory",
                        "BEVERAGE",    "Nescafe Classic instant, Bru instant, Sunrise, David coffee instant",
                        12, SLAB_12, false,
                        "Ground/roasted coffee 5%; instant 12%",
                        "Notif 1/2017-CT(R) Sch-II Sl.90"),

            ("2202",    "Non-alcoholic beverages — juices with sugar, fruit drinks",
                        "BEVERAGE",    "Maaza mango drink, Frooti, Slice, Appy Fizz (non-aerated), paper-boat drinks",
                        12, SLAB_12, false,
                        "Aerated/carbonated drinks 28%+Cess",
                        "Notif 1/2017-CT(R) Sch-II"),

            ("3808",    "Mosquito repellents — mats, coils, liquid vaporiser",
                        "HOUSEHOLD",   "Good Knight mat/liquid, Mortein coil/spray, All Out machine+refill",
                        12, SLAB_12, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-II"),

            ("4818",    "Tissue paper, toilet paper, kitchen towels, napkins",
                        "HOUSEHOLD",   "Kleenex tissues, Scott kitchen towels, Tissue paper rolls",
                        12, SLAB_12, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-II Sl.123"),

            ("3307",    "Pre-shave / after-shave preparations, personal deodorants",
                        "PERSONAL_CARE","Denim after-shave, Old Spice after-shave, Sure deodorant",
                        12, SLAB_12, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-II"),

            // ══════════════════════════════════════════════════════════
            // 18% GST
            // Notification 1/2017-CT(R) Schedule-III
            // ══════════════════════════════════════════════════════════
            ("0403",    "Yoghurt / curd (packaged, branded), buttermilk",
                        "DAIRY",       "Amul Dahi, Mother Dairy curd, Nestle Nesplus yoghurt, Epigamia Greek yoghurt",
                        18, SLAB_18, false,
                        "Loose curd/dahi sold by milkman = 0%",
                        "Notif 1/2017-CT(R) Sch-III Sl.14"),

            ("1806",    "Chocolates, chocolate bars, chocolate confectionery with cocoa",
                        "CONFECTIONERY","Cadbury Dairy Milk, Kit Kat, 5-Star, Munch, Bournville, Milkybar, Perk",
                        18, SLAB_18, false,
                        "White chocolate 18%; cocoa powder without sugar 5%",
                        "Notif 1/2017-CT(R) Sch-III Sl.68"),

            ("1901",    "Malt extract, food preparations of flour/starch/milk",
                        "GROCERY",     "Complan (>50% cocoa), malted beverage mixes, baby cereal foods",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III Sl.73"),

            ("1904",    "Prepared breakfast cereals, cornflakes, muesli, granola, rolled oats (flavored)",
                        "GROCERY",     "Kelloggs Corn Flakes, Kelloggs Muesli, Quaker Oats (flavored), Saffola Muesli",
                        18, SLAB_18, false,
                        "Plain oats/flattened rice without flavoring 5%",
                        "Notif 1/2017-CT(R) Sch-III Sl.76"),

            ("1905",    "Biscuits, cookies, crackers, rusks, wafers, waffle cones",
                        "BAKERY",      "Britannia Bourbon, Parle-G, Marie Gold, Good Day, Oreo, Cream-O, McVities, Monaco, Nimkin, rusk",
                        18, SLAB_18, false,
                        "ALL biscuits 18% (GST Council 47th meeting changed from 12% to 18% for biscuits >₹100/kg; now all 18%)",
                        "Notif 1/2017-CT(R) Sch-III Sl.77"),

            ("2005",    "Prepared vegetables (not vinegar-preserved) — canned/packaged",
                        "GROCERY",     "Canned corn, canned peas, ready-to-eat chana masala, canned mushrooms",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III Sl.83"),

            ("2105",    "Ice cream, frozen desserts, frozen yoghurt",
                        "DAIRY",       "Kwality Walls, Mother Dairy ice cream, Havmor, Baskin Robbins (retail pack), Naturals",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III Sl.91"),

            ("2106.90", "Food preparations NEC — protein powder, energy bars",
                        "HEALTH",      "Protinex, Ensure Plus, whey protein powder, protein bars, Slimfast",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III"),

            ("2201",    "Mineral water / packaged drinking water",
                        "BEVERAGE",    "Bisleri, Kinley, Aquafina, Himalayan, Bailey packaged water",
                        18, SLAB_18, false,
                        "Plain packaged water 18%; aerated water 28%+Cess",
                        "Notif 1/2017-CT(R) Sch-III Sl.93"),

            ("3401",    "Soap bars, liquid soap, hand wash, body wash",
                        "PERSONAL_CARE","Lux, Dove, Lifebuoy, Dettol bar soap, Dettol handwash, Savlon, Pears",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III Sl.166"),

            ("3402",    "Detergent powder, washing powder, liquid detergent, fabric softener",
                        "HOUSEHOLD",   "Surf Excel, Ariel, Rin, Tide, Comfort fabric softener, Vim dishwash bar",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III Sl.167"),

            ("3304",    "Skin care preparations, face cream, fairness cream",
                        "PERSONAL_CARE","Ponds cold cream, Nivea, Lakme, Vaseline petroleum jelly, Fair & Lovely",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III Sl.168"),

            ("3305",    "Hair shampoo, conditioner, hair oil, hair colour",
                        "PERSONAL_CARE","Head & Shoulders, Pantene, Sunsilk, TRESemme, Dabur Almond hair oil, Garnier",
                        18, SLAB_18, false,
                        "Coconut oil sold as hair oil 18%; same coconut oil sold as edible oil 5%",
                        "Notif 1/2017-CT(R) Sch-III Sl.169"),

            ("3306",    "Toothpaste, mouthwash, tooth powder, dental floss",
                        "PERSONAL_CARE","Colgate, Pepsodent, Sensodyne, Oral-B, Listerine, Colgate mouthwash",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III Sl.170"),

            ("3303",    "Perfumes, toilet water, deodorant body spray",
                        "PERSONAL_CARE","Fogg deodorant, Engage deo, Park Avenue, Wild Stone, Axe body spray",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III Sl.167"),

            ("3824",    "Household cleaning agents, disinfectants, floor cleaners",
                        "HOUSEHOLD",   "Lizol, Colin glass cleaner, Harpic toilet cleaner, Phenyl, Domex",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III"),

            ("1704",    "Sugar confectionery (candy, toffee, not containing cocoa)",
                        "CONFECTIONERY","Parle Melody, Mango Bite, Pulse candy, Eclairs, Lollipop",
                        18, SLAB_18, false,
                        "Chewing gum reduced to 5% w.e.f. Jan-2023",
                        "Notif 1/2017-CT(R) Sch-III Sl.64"),

            ("2106.20", "Chips, namkeen, packaged snacks",
                        "SNACKS",      "Lays, Kurkure, Bingo, Haldirams namkeen, Cornitos, Pringles",
                        18, SLAB_18, false,
                        "W.e.f. Oct-2019 all branded namkeen/bhujia 18%",
                        "Notif 13/2019-CT(R)"),

            ("2106.30", "Instant noodles, instant pasta, ready-to-eat packaged food",
                        "GROCERY",     "Maggi 2-minute noodles, Top Ramen, Yippee, Knorr pasta",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III"),

            ("3923",    "Plastic containers, storage boxes, plastic bags",
                        "HOUSEHOLD",   "Tupperware containers, Milton casseroles, plastic airtight jars",
                        18, SLAB_18, false,
                        "",
                        "Notif 1/2017-CT(R) Sch-III"),

            // ══════════════════════════════════════════════════════════
            // 28% GST + CESS — Demerit / Luxury Goods
            // Notification 1/2017-CT(R) Schedule-IV
            // ══════════════════════════════════════════════════════════
            ("2202.10", "Aerated waters, carbonated soft drinks, soda",
                        "BEVERAGE",    "Coca-Cola, Pepsi, Sprite, Fanta, Limca, 7Up, Mountain Dew, Thums Up",
                        28, SLAB_28C, false,
                        "28% GST + 12% Compensation Cess. Total effective rate = 40%",
                        "Notif 1/2017-CT(R) Sch-IV Sl.12 + Cess Notif"),

            ("2402",    "Cigars, cigarettes, cheroots, beedis",
                        "TOBACCO",     "Cigarettes (all brands), cigars",
                        28, SLAB_28C, false,
                        "28% GST + specific cess per cigarette stick (varies by length)",
                        "Notif 1/2017-CT(R) Sch-IV + Cess"),

            ("2403",    "Other tobacco products, pan masala, gutkha, zarda",
                        "TOBACCO",     "Pan masala, gutkha, khaini, zarda",
                        28, SLAB_28C, false,
                        "28% GST + cess; pan masala may face 100%+ cess on certain products",
                        "Notif 1/2017-CT(R) Sch-IV + GST Council 48th Meeting"),
        };

        int count = 0;
        foreach (var e in entries)
        {
            var id   = Guid.NewGuid();
            var cgst = e.exempt ? 0m : e.rate / 2m;
            var sgst = e.exempt ? 0m : e.rate / 2m;
            var igst = e.exempt ? 0m : e.rate;
            var cess = e.slab == SLAB_28C ? 12m : 0m;
            var exemptSql = e.exempt ? "true" : "false";
            var desc    = e.desc.Replace("'", "''");
            var ex      = e.examples.Replace("'", "''");
            var notes   = e.notes.Replace("'", "''");
            var notif   = e.notif.Replace("'", "''");

            await context.Database.ExecuteSqlRawAsync($@"
                INSERT INTO gst_hsn_master_india (
                    id, hsn_code, description, category, example_products,
                    gst_rate_percent, cgst_rate, sgst_rate, igst_rate, cess_rate,
                    is_exempt, notes, notification_ref, tax_slab_id,
                    created_at, is_deleted)
                VALUES (
                    '{id}','{e.hsn}','{desc}','{e.cat}','{ex}',
                    {e.rate},{cgst},{sgst},{igst},{cess},
                    {exemptSql},'{notes}','{notif}','{e.slab}',
                    NOW(),false)
                ON CONFLICT DO NOTHING;
            ");
            count++;
        }

        Console.WriteLine($"[GST] GstHsnMasterIndia seeded: {count} official HSN code entries.");

        if (!await context.Accounts.AnyAsync())
        {
            var accounts = new List<PosErp.Domain.Entities.Finance.Account>
            {
                new() { AccountCode = "1000", Name = "Cash", AccountType = "ASSET" },
                new() { AccountCode = "1100", Name = "Digital/UPI/Card", AccountType = "ASSET" },
                new() { AccountCode = "2100", Name = "Wallet Liability", AccountType = "LIABILITY" },
                new() { AccountCode = "2200", Name = "Output CGST", AccountType = "LIABILITY" },
                new() { AccountCode = "2201", Name = "Output SGST", AccountType = "LIABILITY" },
                new() { AccountCode = "2202", Name = "Output IGST", AccountType = "LIABILITY" },
                new() { AccountCode = "4000", Name = "Sales Revenue", AccountType = "REVENUE" },
                new() { AccountCode = "5000", Name = "Cost of Goods Sold", AccountType = "EXPENSE" }
            };
            context.Accounts.AddRange(accounts);
            await context.SaveChangesAsync();
            Console.WriteLine("[FINANCE] Chart of Accounts seeded.");
        }

        Console.WriteLine("[GST] ═══════════════════════════════════════════════════");
        Console.WriteLine("[GST] GST Slab Reference (India 2024-26):");
        Console.WriteLine("[GST]  0%       — Salt, fresh veg/fruit, unbranded grains, fresh milk, eggs");
        Console.WriteLine("[GST]  5%       — Branded atta/rice/dal, sugar, edible oils, tea, butter");
        Console.WriteLine("[GST] 12%       — Ghee, cheese, pickles, jams, 100% juices, instant coffee");
        Console.WriteLine("[GST] 18%       — Biscuits, chocolates, packaged FMCG, soap, shampoo, detergents");
        Console.WriteLine("[GST] 28%+Cess  — Aerated drinks, tobacco, pan masala");
        Console.WriteLine("[GST] ═══════════════════════════════════════════════════");
    }
}
