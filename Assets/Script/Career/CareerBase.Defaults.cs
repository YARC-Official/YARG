using System;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Song;
using YARG.Helpers;

namespace YARG.Career
{
    public partial class CareerBase
    {
        public static CareerBase[] Defaults => GetDefaults();

        private static CareerBase[] GetDefaults()
        {
            return new[] {
                YargRockCareer(),
                YargSetlistPopCareer(),
            };
        }

        private static CareerBase YargRockCareer()
        {
            // Set 1 [92aa777c-c33d-5eba-a180-a90d591065f4] - 5 charted song(s)
            CareerSong[] tierSet1 = new[]
            {
                new CareerSong(Guid.Parse("a1a97c1d-19ea-48b4-aa2d-8e0bb69c9992")), // Jets Overhead - No Nations
                new CareerSong(Guid.Parse("c0fddcf4-deeb-4b98-8d00-0b98f0ff3882")), // TrackTribe - Guess I'll Never Know
                new CareerSong(Guid.Parse("a1474ea6-ce86-4a86-85c4-8fb596dd8285")), // Jonathan Coulton - Square Things
                new CareerSong(Guid.Parse("bbc23ee6-4336-4fb5-990e-e03a9841bb8c")), // Marta Vega - Oxygen
                new CareerSong(Guid.Parse("dca3cc66-f65d-4709-bcda-eb5250c2e4b7")), // Used to Be Valentines - Beverly
            };

            // Bonus Songs 1 (bonus) [dcf2e5b2-8708-58b2-9c22-ce036d043787] - 10 charted song(s)
            CareerSong[] tierBonusSongs1 = new[]
            {
                new CareerSong(Guid.Parse("f492da61-1cb4-4635-a0c7-099d3d0ea4f2")), // Alohaii - Seasons (feat. Shiki Miyoshino)
                new CareerSong(Guid.Parse("530a0afc-da9c-4951-abdd-9d63df35165a")), // NyxTheShield - Numb the Mind
                new CareerSong(Guid.Parse("7eb17eb8-6379-48f4-a60d-ae61b17571b9")), // The Gang - We Are the Gang
                new CareerSong(Guid.Parse("07eee719-82c0-4874-aa3e-f55a5cb73723")), // Fries on the Side - I'm a Bug
                new CareerSong(Guid.Parse("95319742-18fd-4ed7-b946-f7c6e0647751")), // Tay Zonday - Chocolate Rain (Sweet Like Chocolate Remix)
                new CareerSong(Guid.Parse("00cf27a1-37f9-4234-80c7-d3bca9068227")), // Nana Asteria - Show Ya
                new CareerSong(Guid.Parse("1570dbb7-fbf7-488d-83c4-b8edc8cf3c51")), // Alex Sandra - Over Again
                new CareerSong(Guid.Parse("359d9f75-6219-4fdf-a465-fc52767e444f")), // Portraits of Tracy - The Afterparty
                new CareerSong(Guid.Parse("45faef17-1e4b-4349-a907-d7b0ae0f6b95")), // Red Vox - In the Garden
                new CareerSong(Guid.Parse("11ffcf43-64a5-4caf-9986-d53b78d73e3e")), // Bottom Bunk - Pizza Rolls
            };

            // Set 2 [239b6ac6-ed4b-597a-95fc-14666ba263ea] - 5 charted song(s)
            CareerSong[] tierSet2 = new[]
            {
                new CareerSong(Guid.Parse("4420cda8-b7cd-4f81-a104-3a587ea823ed")), // JW Francis - John, Take Me with You
                new CareerSong(Guid.Parse("d21e5db8-e372-42bf-bbcb-9fb8a0c74462")), // Davvn - Third Degree
                new CareerSong(Guid.Parse("35e9770e-ac25-4339-8ae7-c6abcf89156c")), // Troubled Minds - Buds
                new CareerSong(Guid.Parse("ef6121fa-9aee-4b96-a644-16fba205d4c8")), // Tilly Louise - Join the Club
                new CareerSong(Guid.Parse("06a92c7e-b1db-4159-9471-7c3bc6505155")), // The Covasettes - Duvet Thief
            };

            // Set 3 [512bf8db-e5d3-58f3-b961-a27114a633b4] - 5 charted song(s)
            CareerSong[] tierSet3 = new[]
            {
                new CareerSong(Guid.Parse("56390e0b-debb-4743-a1ba-0e8a401af492")), // Last Dinosaurs - Eleven
                new CareerSong(Guid.Parse("2c69588b-c01d-4df9-ac7f-1dc0fe5884a1")), // King Gizzard and the Lizard Wizard - Plastic Boogie
                new CareerSong(Guid.Parse("ece8093f-07f3-49ea-9111-efe6776c67c2")), // Vessel - Overdrive
                new CareerSong(Guid.Parse("815e8f4d-6568-437a-86e4-cef5e3ab3f95")), // Nine Inch Nails - Discipline
                new CareerSong(Guid.Parse("9e3f47e3-7d12-4399-8a48-3e47155b6c15")), // Bob Kulick and David Glen Eisley - Sweet Victory
            };

            // Bonus Songs 2 (bonus) [560ef7da-a2ad-5485-9c15-fdb9c5057de8] - 10 charted song(s)
            CareerSong[] tierBonusSongs2 = new[]
            {
                new CareerSong(Guid.Parse("b5e52cdb-914d-4a56-8e08-675920d549cd")), // Heartsick - Sadness (feat. Sapphire Noel)
                new CareerSong(Guid.Parse("2672fd8a-574e-4bd2-8429-87fe57967226")), // FIG - Splinter
                new CareerSong(Guid.Parse("c1b450e0-a3cc-44b0-b048-ba4550c657bf")), // Roomie - It Kills Me
                new CareerSong(Guid.Parse("902cb8e9-3e51-4894-b3d9-cdb3726b7ff6")), // Daniel Thrasher - Igowallah
                new CareerSong(Guid.Parse("c2ab2b15-edc9-40fe-9828-bd9503f4c09d")), // The Sterns - About the Author
                new CareerSong(Guid.Parse("94f4125f-c30a-4807-a69f-4f6b507026c8")), // WhyLucas - Oh, Krissy Baby!
                new CareerSong(Guid.Parse("8287d828-ee4e-4bc7-bfad-ea4afbfc5b12")), // Glass Beach - Bedroom Community
                new CareerSong(Guid.Parse("8865327b-6f77-46ab-beba-f511db7cf3a0")), // Thanks! I Hate It - Participation Trophy Wife
                new CareerSong(Guid.Parse("135083a3-4e62-4aea-b17c-efc99e34d444")), // Vandalheart - Time
                new CareerSong(Guid.Parse("35094d70-ad1f-47e7-9b97-9e8a23ac697c")), // Boom Kitty - Boom Slayer (feat. Scott Foster Harris)
            };

            // Set 4 [76c85f05-30f7-57a0-8e2b-48ec97613929] - 5 charted song(s)
            CareerSong[] tierSet4 = new[]
            {
                new CareerSong(Guid.Parse("e1cdf591-ab4f-4f7f-944f-4874e50d774d")), // Adamic - All of a Sudden
                new CareerSong(Guid.Parse("783e6fa5-2d5f-4a8c-8004-3172830d7f8e")), // Another One Down! - Exeter
                new CareerSong(Guid.Parse("3e7849c4-bac4-4749-bfca-e5ccb349bef9")), // Thousand Thoughts - Circles
                new CareerSong(Guid.Parse("7544dbdb-4eda-4a77-8e59-bb6c235504cb")), // Carter Vail - I Don't Wanna Talk
                new CareerSong(Guid.Parse("8d3137f4-373a-413d-940c-dc0b2338c1ac")), // NateWantsToBattle - To Let Go
            };

            // Set 5 [c0ef3129-c420-544b-ae68-d4b4edc4a9cc] - 5 charted song(s)
            CareerSong[] tierSet5 = new[]
            {
                new CareerSong(Guid.Parse("bfced079-86df-4720-b7ae-c98f3508120b")), // Telethon - Positively Clark Street
                new CareerSong(Guid.Parse("42d0e0f8-7767-4148-8cfc-1b5eb2c529d6")), // Saturdays at Your Place - Eat Me Alive
                new CareerSong(Guid.Parse("abe11359-0565-4b39-85de-221d06c0cba4")), // Good Kid - Nomu
                new CareerSong(Guid.Parse("5a69ed2c-da43-45e8-9551-c6f218a5ac05")), // Kingseeker - Butterflies
                new CareerSong(Guid.Parse("05a9f3fc-64d6-41f4-9289-e75299254e74")), // Gnarlemagne - Ain't Askin for Much
            };

            // Bonus Songs 3 (bonus) [095ae288-b533-50ac-81ae-daee9c29e9fe] - 10 charted song(s)
            CareerSong[] tierBonusSongs3 = new[]
            {
                new CareerSong(Guid.Parse("6c379b52-b62a-4882-b07d-ed80ec630d51")), // See You at Rogers - Choked Up
                new CareerSong(Guid.Parse("065a1e09-7735-4cb2-a633-01b764c3743a")), // Prismia - Don't Look!
                new CareerSong(Guid.Parse("5683c3f8-d251-490c-a46d-f0efb73743ef")), // Stone Deep - Runnin Man
                new CareerSong(Guid.Parse("3153a013-5cdc-457e-85a2-5de19b46e33f")), // Kidd Judo - Don't Spook the Owl
                new CareerSong(Guid.Parse("dc2982c7-a72c-4fdf-8056-8813203f6264")), // Scro - Is This What You Wanted
                new CareerSong(Guid.Parse("6119a336-1d5b-40ea-a5ff-fbddcc14c4f3")), // LilDeuceDeuce - Everybody Do the Flop
                new CareerSong(Guid.Parse("78496b73-17c6-434c-a8ef-7a0687fd6d91")), // Jamie Paige - I Wish That I Could Fall (feat. GUMI)
                new CareerSong(Guid.Parse("5e961740-ab28-4432-99a3-4323a00af058")), // The Vanished People - Queen of the Night
                new CareerSong(Guid.Parse("96f805fb-379e-4388-a3d8-d277ac0126a6")), // Hadson - Human God
                new CareerSong(Guid.Parse("a4f4eefc-b14f-4569-b52f-98496fe5a8ef")), // Cantervice - The Masquerade
            };

            // Set 6 [54b25571-bcbd-54c5-8950-ddf5784c538e] - 5 charted song(s)
            CareerSong[] tierSet6 = new[]
            {
                new CareerSong(Guid.Parse("af0b1b71-10ca-493e-99c3-6256633340f3")), // RinRin - The Game
                new CareerSong(Guid.Parse("c0d6bd2c-b49a-4464-bb43-3de18fef6366")), // Luminism - Poser
                new CareerSong(Guid.Parse("baa78ac2-9f45-408a-8b9b-e19bd1219e3e")), // Palette Knife - Avatar the Last Cakebender
                new CareerSong(Guid.Parse("99d7b85c-306c-4376-a363-7ec682ed5aa3")), // Surefire - Salt the Wound
                new CareerSong(Guid.Parse("a370639a-64b9-4f4f-a054-07754a8632a2")), // Atomic Guava - Cowboy Tanaka
            };

            // Set 7 [02f5d98e-82f2-536f-9ea0-8e9202b2e03a] - 5 charted song(s)
            CareerSong[] tierSet7 = new[]
            {
                new CareerSong(Guid.Parse("1f36bed1-8e79-40fc-aabb-bce036b03903")), // Auris - They Call
                new CareerSong(Guid.Parse("ffcd4b9a-c0a2-4546-94a6-5bf60c035a19")), // Vera Kay - Alibi
                new CareerSong(Guid.Parse("ebc88cb0-88ec-4797-8181-42f31231beb4")), // Jim's Big Ego - Stress
                new CareerSong(Guid.Parse("5c3161e9-099b-446e-985b-d93d90279ab4")), // Frostbitt - 106
                new CareerSong(Guid.Parse("395b26a6-9f7a-4f81-9005-1b16d3ce65f9")), // Space Weather - Dancing Demons
            };

            // Bonus Songs 4 (bonus) [a38b05e2-641f-56c1-8363-bd43309ba918] - 10 charted song(s)
            CareerSong[] tierBonusSongs4 = new[]
            {
                new CareerSong(Guid.Parse("1fb2ce9a-4f52-40e6-8a23-df02b6136308")), // Frantic Memories - Song of November
                new CareerSong(Guid.Parse("54a0e0e2-1fca-4e17-985d-0d442e93e2c1")), // Flying Raccoon Suit - Long in the Tooth
                new CareerSong(Guid.Parse("a2d0564b-d1d7-4ed6-9af3-3912528341e1")), // Snail's House - Pixel Galaxy
                new CareerSong(Guid.Parse("cca18aa1-8f23-45a3-b605-e2b6334740a7")), // Garlagan - Do
                new CareerSong(Guid.Parse("15d01f78-486a-4324-aa1d-593e1378c653")), // Creo - 322
                new CareerSong(Guid.Parse("377a0226-9491-40a4-a842-753125de332b")), // RO1 - A Visitant (feat. Victor Borba)
                new CareerSong(Guid.Parse("302702bf-d0e4-4eb2-b73e-dc319086f4e4")), // Our Common Collapse - Languish
                new CareerSong(Guid.Parse("27c78069-f30f-4767-bb74-d22683c0a2ae")), // Tanger - Strangers Once Again (feat. Treb and Ofir Takabov) [aliased title]
                new CareerSong(Guid.Parse("fc183679-f59f-4861-893f-0c6f0b70cede")), // White Coven - Rambling Rose
                new CareerSong(Guid.Parse("0155ec33-35c8-4a4f-b415-e4b26870e474")), // Bitbreaker - God Only Knows (feat. Kasane Teto)
            };

            // Set 8 [89904050-9cd4-54f4-adb2-fb038dd006a8] - 5 charted song(s)
            CareerSong[] tierSet8 = new[]
            {
                new CareerSong(Guid.Parse("9647a6b9-7cd4-48f2-b5f7-42da96d156c6")), // Blight Town - Al Gore Rhythm
                new CareerSong(Guid.Parse("f6a4fa3b-518b-41b7-a46a-7e7dda58b7cc")), // Groundlift - Bottleneck
                new CareerSong(Guid.Parse("5c13bf2c-7830-43df-a64e-d8529d418722")), // Four Year Strong - We All Float Down Here
                new CareerSong(Guid.Parse("0b1e8054-65f6-4db3-81b4-ed30b8030609")), // Prototype - Synthespian
                new CareerSong(Guid.Parse("43ea77fb-adcf-470e-873d-0b88ce386a0e")), // Krilloan - Emperor Rising
            };

            // Career Finale [58a12af9-dd41-5d32-9a35-f4a124cc40f8] - 1 charted song(s)
            CareerSong[] tierCareerFinale = new[]
            {
                new CareerSong(Guid.Parse("7a66dafc-a150-499d-898d-9ad0f069a411")), // Höwler - The New World Disorder
            };

            // Boss Songs [a9ac9674-48b2-55c2-b3ed-aec99d7ebfce] - 9 charted song(s), 1 unmatched
            CareerSong[] tierBossSongs = new[]
            {
                new CareerSong(Guid.Parse("f985a4a9-7aad-4465-86ca-9d8b76145f07")), // insaneintherainmusic - Luminaire
                new CareerSong(Guid.Parse("831dcf73-77d0-41a0-bedd-57bf7227bc7a")), // Bumblefoot - Jenny B
                new CareerSong(Guid.Parse("4a8c91f5-e7da-4933-ac3c-58a2556dc1c9")), // Max Boras - Frank Scored a Video Game
                new CareerSong(Guid.Parse("ea858f8c-69f5-43c4-88ae-f0ebfa1ca074")), // 8-Bit Big Band - Moonlight Sonata 3rd (Big Band Version) [aliased title]
                new CareerSong(Guid.Parse("82d64037-ecf8-430b-9b5c-dfb800196edb")), // FamilyJules - Flight of the Bumblebee
                new CareerSong(Guid.Parse("41955597-c1ed-4805-9931-c74ca5c7c945")), // Camellia - 1nput This 2 Y0ur Spine
                new CareerSong(Guid.Parse("2c8b4065-b608-4f29-81b2-551fc3d720cd")), // obkatiekat - Tick Tock
                new CareerSong(Guid.Parse("72884d6c-5af5-4247-adcf-5140ea2545b1")), // A Pretext to Human Suffering - Formless Collective
                new CareerSong(Guid.Parse("8e2cf75f-5272-400d-af33-d1fdb3213af4")), // Mindiode - Mass Gap
            };

            // Sidequest Songs (bonus) [35910a9f-c468-55d9-a92e-6fd64e85ed6b] - 8 charted song(s), 6 unmatched
            CareerSong[] tierSidequestSongs = new[]
            {
                new CareerSong(Guid.Parse("f2f32625-8362-4824-a457-31e365d032c6")), // Pinegrove - Need 2
                new CareerSong(Guid.Parse("9b35574b-5557-493c-b9e9-bfbf9f264b07")), // The Polarity - Love Me Pls
                new CareerSong(Guid.Parse("b529d933-90cb-4f3b-86ea-50134948344e")), // State Champs - Secrets
                new CareerSong(Guid.Parse("7fadd9a9-347b-4134-b635-86ceac65af35")), // CircusP - Better Off Worse (feat. Flower)
                new CareerSong(Guid.Parse("eb0354b7-7687-4ea1-a9ab-f04fcaa3530f")), // Lemon Demon - Fine
                new CareerSong(Guid.Parse("4f2ccdfb-4916-4333-a3cc-a948e639c7fb")), // JubyPhonic - Oopsie Daisy
                new CareerSong(Guid.Parse("7ad055cd-84df-496a-9c01-49a1f494b879")), // InVerSe - Blue (feat. Miori Celesta)
                new CareerSong(Guid.Parse("44fc0692-9d15-4cab-9156-05432ad7dc7d")), // Electric Swing Circus - Empires
            };

            CareerTier[] tiers =
            {
                new()
                {
                    Id = new Guid("92aa777c-c33d-5eba-a180-a90d591065f4"),
                    Name = "Set 1",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 0,
                    Songs = tierSet1,
                },
                new()
                {
                    Id = new Guid("dcf2e5b2-8708-58b2-9c22-ce036d043787"),
                    Name = "Bonus Songs 1",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 25,
                    Songs = tierBonusSongs1,
                    IsBonus = true,
                },
                new()
                {
                    Id = new Guid("239b6ac6-ed4b-597a-95fc-14666ba263ea"),
                    Name = "Set 2",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 25,
                    Songs = tierSet2,
                },
                new()
                {
                    Id = new Guid("512bf8db-e5d3-58f3-b961-a27114a633b4"),
                    Name = "Set 3",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 50,
                    Songs = tierSet3,
                },
                new()
                {
                    Id = new Guid("560ef7da-a2ad-5485-9c15-fdb9c5057de8"),
                    Name = "Bonus Songs 2",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 75,
                    Songs = tierBonusSongs2,
                    IsBonus = true,
                },
                new()
                {
                    Id = new Guid("76c85f05-30f7-57a0-8e2b-48ec97613929"),
                    Name = "Set 4",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 75,
                    Songs = tierSet4,
                },
                new()
                {
                    Id = new Guid("c0ef3129-c420-544b-ae68-d4b4edc4a9cc"),
                    Name = "Set 5",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 100,
                    Songs = tierSet5,
                },
                new()
                {
                    Id = new Guid("095ae288-b533-50ac-81ae-daee9c29e9fe"),
                    Name = "Bonus Songs 3",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 125,
                    Songs = tierBonusSongs3,
                    IsBonus = true,
                },
                new()
                {
                    Id = new Guid("54b25571-bcbd-54c5-8950-ddf5784c538e"),
                    Name = "Set 6",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 125,
                    Songs = tierSet6,
                },
                new()
                {
                    Id = new Guid("02f5d98e-82f2-536f-9ea0-8e9202b2e03a"),
                    Name = "Set 7",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 150,
                    Songs = tierSet7,
                },
                new()
                {
                    Id = new Guid("a38b05e2-641f-56c1-8363-bd43309ba918"),
                    Name = "Bonus Songs 4",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 175,
                    Songs = tierBonusSongs4,
                    IsBonus = true,
                },
                new()
                {
                    Id = new Guid("89904050-9cd4-54f4-adb2-fb038dd006a8"),
                    Name = "Set 8",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 175,
                    Songs = tierSet8,
                },
                new()
                {
                    Id = new Guid("58a12af9-dd41-5d32-9a35-f4a124cc40f8"),
                    Name = "Career Finale",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 200,
                    Songs = tierCareerFinale,
                },
                new()
                {
                    Id = new Guid("a9ac9674-48b2-55c2-b3ed-aec99d7ebfce"),
                    Name = "Boss Songs",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 205,
                    Songs = tierBossSongs,
                },
                new()
                {
                    Id = new Guid("35910a9f-c468-55d9-a92e-6fd64e85ed6b"),
                    Name = "Sidequest Songs",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 250,
                    Songs = tierSidequestSongs,
                },
            };

            return new CareerBase(
                new Guid("1cb75c48-ef17-52aa-bb40-f48fe0fcf2d4"),
                "YARG Setlist Rock",
                "Maybe you can be a rock star?",
                null,
                "yarg",
                true,
                tiers);
        }

        private static CareerBase YargSetlistPopCareer()
        {
            // Set 1 [92aa777c-c33d-5eba-a180-a90d591065f4] - 5 charted song(s)
            CareerSong[] tierSet1 = new[]
            {
                new CareerSong(Guid.Parse("a1a97c1d-19ea-48b4-aa2d-8e0bb69c9992")), // Jets Overhead - No Nations
                new CareerSong(Guid.Parse("c0fddcf4-deeb-4b98-8d00-0b98f0ff3882")), // TrackTribe - Guess I'll Never Know
                new CareerSong(Guid.Parse("f492da61-1cb4-4635-a0c7-099d3d0ea4f2")), // Alohaii - Seasons (feat. Shiki Miyoshino)
                new CareerSong(Guid.Parse("a1474ea6-ce86-4a86-85c4-8fb596dd8285")), // Jonathan Coulton - Square Things
                new CareerSong(Guid.Parse("00cf27a1-37f9-4234-80c7-d3bca9068227")), // Nana Asteria - Show Ya
            };

            // Bonus Songs 1 (bonus) [dcf2e5b2-8708-58b2-9c22-ce036d043787] - 10 charted song(s)
            CareerSong[] tierBonusSongs1 = new[]
            {
                new CareerSong(Guid.Parse("f2f32625-8362-4824-a457-31e365d032c6")), // Pinegrove - Need 2
                new CareerSong(Guid.Parse("530a0afc-da9c-4951-abdd-9d63df35165a")), // NyxTheShield - Numb the Mind
                new CareerSong(Guid.Parse("4f2ccdfb-4916-4333-a3cc-a948e639c7fb")), // JubyPhonic - Oopsie Daisy
                new CareerSong(Guid.Parse("95319742-18fd-4ed7-b946-f7c6e0647751")), // Tay Zonday - Chocolate Rain (Sweet Like Chocolate Remix)
                new CareerSong(Guid.Parse("2c69588b-c01d-4df9-ac7f-1dc0fe5884a1")), // King Gizzard and the Lizard Wizard - Plastic Boogie
                new CareerSong(Guid.Parse("7eb17eb8-6379-48f4-a60d-ae61b17571b9")), // The Gang - We Are the Gang
                new CareerSong(Guid.Parse("35e9770e-ac25-4339-8ae7-c6abcf89156c")), // Troubled Minds - Buds
                new CareerSong(Guid.Parse("11ffcf43-64a5-4caf-9986-d53b78d73e3e")), // Bottom Bunk - Pizza Rolls
                new CareerSong(Guid.Parse("dca3cc66-f65d-4709-bcda-eb5250c2e4b7")), // Used to Be Valentines - Beverly
                new CareerSong(Guid.Parse("07eee719-82c0-4874-aa3e-f55a5cb73723")), // Fries on the Side - I'm a Bug
            };

            // Set 2 [239b6ac6-ed4b-597a-95fc-14666ba263ea] - 5 charted song(s)
            CareerSong[] tierSet2 = new[]
            {
                new CareerSong(Guid.Parse("bbc23ee6-4336-4fb5-990e-e03a9841bb8c")), // Marta Vega - Oxygen
                new CareerSong(Guid.Parse("45faef17-1e4b-4349-a907-d7b0ae0f6b95")), // Red Vox - In the Garden
                new CareerSong(Guid.Parse("359d9f75-6219-4fdf-a465-fc52767e444f")), // Portraits of Tracy - The Afterparty
                new CareerSong(Guid.Parse("b5e52cdb-914d-4a56-8e08-675920d549cd")), // Heartsick - Sadness (feat. Sapphire Noel)
                new CareerSong(Guid.Parse("06a92c7e-b1db-4159-9471-7c3bc6505155")), // The Covasettes - Duvet Thief
            };

            // Set 3 [512bf8db-e5d3-58f3-b961-a27114a633b4] - 5 charted song(s)
            CareerSong[] tierSet3 = new[]
            {
                new CareerSong(Guid.Parse("2672fd8a-574e-4bd2-8429-87fe57967226")), // FIG - Splinter
                new CareerSong(Guid.Parse("815e8f4d-6568-437a-86e4-cef5e3ab3f95")), // Nine Inch Nails - Discipline
                new CareerSong(Guid.Parse("ef6121fa-9aee-4b96-a644-16fba205d4c8")), // Tilly Louise - Join the Club
                new CareerSong(Guid.Parse("94f4125f-c30a-4807-a69f-4f6b507026c8")), // WhyLucas - Oh, Krissy Baby!
                new CareerSong(Guid.Parse("9e3f47e3-7d12-4399-8a48-3e47155b6c15")), // Bob Kulick and David Glen Eisley - Sweet Victory
            };

            // Bonus Songs 2 (bonus) [560ef7da-a2ad-5485-9c15-fdb9c5057de8] - 10 charted song(s)
            CareerSong[] tierBonusSongs2 = new[]
            {
                new CareerSong(Guid.Parse("1570dbb7-fbf7-488d-83c4-b8edc8cf3c51")), // Alex Sandra - Over Again
                new CareerSong(Guid.Parse("56390e0b-debb-4743-a1ba-0e8a401af492")), // Last Dinosaurs - Eleven
                new CareerSong(Guid.Parse("4420cda8-b7cd-4f81-a104-3a587ea823ed")), // JW Francis - John, Take Me with You
                new CareerSong(Guid.Parse("902cb8e9-3e51-4894-b3d9-cdb3726b7ff6")), // Daniel Thrasher - Igowallah
                new CareerSong(Guid.Parse("bfced079-86df-4720-b7ae-c98f3508120b")), // Telethon - Positively Clark Street
                new CareerSong(Guid.Parse("8865327b-6f77-46ab-beba-f511db7cf3a0")), // Thanks! I Hate It - Participation Trophy Wife
                new CareerSong(Guid.Parse("783e6fa5-2d5f-4a8c-8004-3172830d7f8e")), // Another One Down! - Exeter
                new CareerSong(Guid.Parse("3e7849c4-bac4-4749-bfca-e5ccb349bef9")), // Thousand Thoughts - Circles
                new CareerSong(Guid.Parse("135083a3-4e62-4aea-b17c-efc99e34d444")), // Vandalheart - Time
                new CareerSong(Guid.Parse("8d3137f4-373a-413d-940c-dc0b2338c1ac")), // NateWantsToBattle - To Let Go
            };

            // Set 4 [76c85f05-30f7-57a0-8e2b-48ec97613929] - 5 charted song(s)
            CareerSong[] tierSet4 = new[]
            {
                new CareerSong(Guid.Parse("d21e5db8-e372-42bf-bbcb-9fb8a0c74462")), // Davvn - Third Degree
                new CareerSong(Guid.Parse("7544dbdb-4eda-4a77-8e59-bb6c235504cb")), // Carter Vail - I Don't Wanna Talk
                new CareerSong(Guid.Parse("c2ab2b15-edc9-40fe-9828-bd9503f4c09d")), // The Sterns - About the Author
                new CareerSong(Guid.Parse("c1b450e0-a3cc-44b0-b048-ba4550c657bf")), // Roomie - It Kills Me
                new CareerSong(Guid.Parse("8287d828-ee4e-4bc7-bfad-ea4afbfc5b12")), // Glass Beach - Bedroom Community
            };

            // Set 5 [c0ef3129-c420-544b-ae68-d4b4edc4a9cc] - 5 charted song(s)
            CareerSong[] tierSet5 = new[]
            {
                new CareerSong(Guid.Parse("44fc0692-9d15-4cab-9156-05432ad7dc7d")), // Electric Swing Circus - Empires
                new CareerSong(Guid.Parse("35094d70-ad1f-47e7-9b97-9e8a23ac697c")), // Boom Kitty - Boom Slayer (feat. Scott Foster Harris)
                new CareerSong(Guid.Parse("7fadd9a9-347b-4134-b635-86ceac65af35")), // CircusP - Better Off Worse (feat. Flower)
                new CareerSong(Guid.Parse("dc2982c7-a72c-4fdf-8056-8813203f6264")), // Scro - Is This What You Wanted
                new CareerSong(Guid.Parse("05a9f3fc-64d6-41f4-9289-e75299254e74")), // Gnarlemagne - Ain't Askin for Much
            };

            // Bonus Songs 3 (bonus) [095ae288-b533-50ac-81ae-daee9c29e9fe] - 10 charted song(s)
            CareerSong[] tierBonusSongs3 = new[]
            {
                new CareerSong(Guid.Parse("6c379b52-b62a-4882-b07d-ed80ec630d51")), // See You at Rogers - Choked Up
                new CareerSong(Guid.Parse("eb0354b7-7687-4ea1-a9ab-f04fcaa3530f")), // Lemon Demon - Fine
                new CareerSong(Guid.Parse("9b35574b-5557-493c-b9e9-bfbf9f264b07")), // The Polarity - Love Me Pls
                new CareerSong(Guid.Parse("c0d6bd2c-b49a-4464-bb43-3de18fef6366")), // Luminism - Poser
                new CareerSong(Guid.Parse("5683c3f8-d251-490c-a46d-f0efb73743ef")), // Stone Deep - Runnin Man
                new CareerSong(Guid.Parse("ece8093f-07f3-49ea-9111-efe6776c67c2")), // Vessel - Overdrive
                new CareerSong(Guid.Parse("3153a013-5cdc-457e-85a2-5de19b46e33f")), // Kidd Judo - Don't Spook the Owl
                new CareerSong(Guid.Parse("5a69ed2c-da43-45e8-9551-c6f218a5ac05")), // Kingseeker - Butterflies
                new CareerSong(Guid.Parse("af0b1b71-10ca-493e-99c3-6256633340f3")), // RinRin - The Game
                new CareerSong(Guid.Parse("1fb2ce9a-4f52-40e6-8a23-df02b6136308")), // Frantic Memories - Song of November
            };

            // Set 6 [54b25571-bcbd-54c5-8950-ddf5784c538e] - 5 charted song(s)
            CareerSong[] tierSet6 = new[]
            {
                new CareerSong(Guid.Parse("065a1e09-7735-4cb2-a633-01b764c3743a")), // Prismia - Don't Look!
                new CareerSong(Guid.Parse("78496b73-17c6-434c-a8ef-7a0687fd6d91")), // Jamie Paige - I Wish That I Could Fall (feat. GUMI)
                new CareerSong(Guid.Parse("5e961740-ab28-4432-99a3-4323a00af058")), // The Vanished People - Queen of the Night
                new CareerSong(Guid.Parse("a4f4eefc-b14f-4569-b52f-98496fe5a8ef")), // Cantervice - The Masquerade
                new CareerSong(Guid.Parse("377a0226-9491-40a4-a842-753125de332b")), // RO1 - A Visitant (feat. Victor Borba)
            };

            // Set 7 [02f5d98e-82f2-536f-9ea0-8e9202b2e03a] - 5 charted song(s)
            CareerSong[] tierSet7 = new[]
            {
                new CareerSong(Guid.Parse("a370639a-64b9-4f4f-a054-07754a8632a2")), // Atomic Guava - Cowboy Tanaka
                new CareerSong(Guid.Parse("baa78ac2-9f45-408a-8b9b-e19bd1219e3e")), // Palette Knife - Avatar the Last Cakebender
                new CareerSong(Guid.Parse("cca18aa1-8f23-45a3-b605-e2b6334740a7")), // Garlagan - Do
                new CareerSong(Guid.Parse("54a0e0e2-1fca-4e17-985d-0d442e93e2c1")), // Flying Raccoon Suit - Long in the Tooth
                new CareerSong(Guid.Parse("ffcd4b9a-c0a2-4546-94a6-5bf60c035a19")), // Vera Kay - Alibi
            };

            // Bonus Songs 4 (bonus) [a38b05e2-641f-56c1-8363-bd43309ba918] - 10 charted song(s)
            CareerSong[] tierBonusSongs4 = new[]
            {
                new CareerSong(Guid.Parse("96f805fb-379e-4388-a3d8-d277ac0126a6")), // Hadson - Human God
                new CareerSong(Guid.Parse("a2d0564b-d1d7-4ed6-9af3-3912528341e1")), // Snail's House - Pixel Galaxy
                new CareerSong(Guid.Parse("ebc88cb0-88ec-4797-8181-42f31231beb4")), // Jim's Big Ego - Stress
                new CareerSong(Guid.Parse("5c3161e9-099b-446e-985b-d93d90279ab4")), // Frostbitt - 106
                new CareerSong(Guid.Parse("302702bf-d0e4-4eb2-b73e-dc319086f4e4")), // Our Common Collapse - Languish
                new CareerSong(Guid.Parse("fc183679-f59f-4861-893f-0c6f0b70cede")), // White Coven - Rambling Rose
                new CareerSong(Guid.Parse("395b26a6-9f7a-4f81-9005-1b16d3ce65f9")), // Space Weather - Dancing Demons
                new CareerSong(Guid.Parse("f6a4fa3b-518b-41b7-a46a-7e7dda58b7cc")), // Groundlift - Bottleneck
                new CareerSong(Guid.Parse("0b1e8054-65f6-4db3-81b4-ed30b8030609")), // Prototype - Synthespian
                new CareerSong(Guid.Parse("43ea77fb-adcf-470e-873d-0b88ce386a0e")), // Krilloan - Emperor Rising
            };

            // Set 8 [89904050-9cd4-54f4-adb2-fb038dd006a8] - 5 charted song(s)
            CareerSong[] tierSet8 = new[]
            {
                new CareerSong(Guid.Parse("15d01f78-486a-4324-aa1d-593e1378c653")), // Creo - 322
                new CareerSong(Guid.Parse("6119a336-1d5b-40ea-a5ff-fbddcc14c4f3")), // LilDeuceDeuce - Everybody Do the Flop
                new CareerSong(Guid.Parse("27c78069-f30f-4767-bb74-d22683c0a2ae")), // Tanger - Strangers Once Again (feat. Treb and Ofir Takabov) [aliased title]
                new CareerSong(Guid.Parse("7ad055cd-84df-496a-9c01-49a1f494b879")), // InVerSe - Blue (feat. Miori Celesta)
                new CareerSong(Guid.Parse("0155ec33-35c8-4a4f-b415-e4b26870e474")), // Bitbreaker - God Only Knows (feat. Kasane Teto)
            };

            // Career Finale [58a12af9-dd41-5d32-9a35-f4a124cc40f8] - 0 charted song(s), 1 unmatched
            CareerSong[] tierCareerFinale = new CareerSong[]
            {
                // TODO no chart in master source: [REDACTED]
            };

            // Boss Songs [a9ac9674-48b2-55c2-b3ed-aec99d7ebfce] - 10 charted song(s)
            CareerSong[] tierBossSongs = new[]
            {
                new CareerSong(Guid.Parse("f985a4a9-7aad-4465-86ca-9d8b76145f07")), // insaneintherainmusic - Luminaire
                new CareerSong(Guid.Parse("831dcf73-77d0-41a0-bedd-57bf7227bc7a")), // Bumblefoot - Jenny B
                new CareerSong(Guid.Parse("4a8c91f5-e7da-4933-ac3c-58a2556dc1c9")), // Max Boras - Frank Scored a Video Game
                new CareerSong(Guid.Parse("ea858f8c-69f5-43c4-88ae-f0ebfa1ca074")), // 8-Bit Big Band - Moonlight Sonata 3rd (Big Band Version) [aliased title]
                new CareerSong(Guid.Parse("82d64037-ecf8-430b-9b5c-dfb800196edb")), // FamilyJules - Flight of the Bumblebee
                new CareerSong(Guid.Parse("7a66dafc-a150-499d-898d-9ad0f069a411")), // Höwler - The New World Disorder
                new CareerSong(Guid.Parse("41955597-c1ed-4805-9931-c74ca5c7c945")), // Camellia - 1nput This 2 Y0ur Spine
                new CareerSong(Guid.Parse("2c8b4065-b608-4f29-81b2-551fc3d720cd")), // obkatiekat - Tick Tock
                new CareerSong(Guid.Parse("72884d6c-5af5-4247-adcf-5140ea2545b1")), // A Pretext to Human Suffering - Formless Collective
                new CareerSong(Guid.Parse("8e2cf75f-5272-400d-af33-d1fdb3213af4")), // Mindiode - Mass Gap
            };

            // Sidequest Songs (bonus) [35910a9f-c468-55d9-a92e-6fd64e85ed6b] - 8 charted song(s), 6 unmatched
            CareerSong[] tierSidequestSongs = new[]
            {
                new CareerSong(Guid.Parse("42d0e0f8-7767-4148-8cfc-1b5eb2c529d6")), // Saturdays at Your Place - Eat Me Alive
                new CareerSong(Guid.Parse("b529d933-90cb-4f3b-86ea-50134948344e")), // State Champs - Secrets
                new CareerSong(Guid.Parse("99d7b85c-306c-4376-a363-7ec682ed5aa3")), // Surefire - Salt the Wound
                new CareerSong(Guid.Parse("1f36bed1-8e79-40fc-aabb-bce036b03903")), // Auris - They Call
                new CareerSong(Guid.Parse("5c13bf2c-7830-43df-a64e-d8529d418722")), // Four Year Strong - We All Float Down Here
                new CareerSong(Guid.Parse("e1cdf591-ab4f-4f7f-944f-4874e50d774d")), // Adamic - All of a Sudden
                new CareerSong(Guid.Parse("9647a6b9-7cd4-48f2-b5f7-42da96d156c6")), // Blight Town - Al Gore Rhythm
                new CareerSong(Guid.Parse("abe11359-0565-4b39-85de-221d06c0cba4")), // Good Kid - Nomu
                // TODO no chart in master source: [REDACTED]
                // TODO no chart in master source: [REDACTED]
                // TODO no chart in master source: [REDACTED]
                // TODO no chart in master source: [REDACTED]
                // TODO no chart in master source: [REDACTED]
                // TODO no chart in master source: [REDACTED]
            };

            CareerTier[] tiers =
            {
                new()
                {
                    Id = new Guid("92aa777c-c33d-5eba-a180-a90d591065f4"),
                    Name = "Set 1",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 0,
                    Songs = tierSet1,
                },
                new()
                {
                    Id = new Guid("dcf2e5b2-8708-58b2-9c22-ce036d043787"),
                    Name = "Bonus Songs 1",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 25,
                    Songs = tierBonusSongs1,
                    IsBonus = true,
                },
                new()
                {
                    Id = new Guid("239b6ac6-ed4b-597a-95fc-14666ba263ea"),
                    Name = "Set 2",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 25,
                    Songs = tierSet2,
                },
                new()
                {
                    Id = new Guid("512bf8db-e5d3-58f3-b961-a27114a633b4"),
                    Name = "Set 3",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 50,
                    Songs = tierSet3,
                },
                new()
                {
                    Id = new Guid("560ef7da-a2ad-5485-9c15-fdb9c5057de8"),
                    Name = "Bonus Songs 2",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 75,
                    Songs = tierBonusSongs2,
                    IsBonus = true,
                },
                new()
                {
                    Id = new Guid("76c85f05-30f7-57a0-8e2b-48ec97613929"),
                    Name = "Set 4",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 75,
                    Songs = tierSet4,
                },
                new()
                {
                    Id = new Guid("c0ef3129-c420-544b-ae68-d4b4edc4a9cc"),
                    Name = "Set 5",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 100,
                    Songs = tierSet5,
                },
                new()
                {
                    Id = new Guid("095ae288-b533-50ac-81ae-daee9c29e9fe"),
                    Name = "Bonus Songs 3",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 125,
                    Songs = tierBonusSongs3,
                    IsBonus = true,
                },
                new()
                {
                    Id = new Guid("54b25571-bcbd-54c5-8950-ddf5784c538e"),
                    Name = "Set 6",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 125,
                    Songs = tierSet6,
                },
                new()
                {
                    Id = new Guid("02f5d98e-82f2-536f-9ea0-8e9202b2e03a"),
                    Name = "Set 7",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 150,
                    Songs = tierSet7,
                },
                new()
                {
                    Id = new Guid("a38b05e2-641f-56c1-8363-bd43309ba918"),
                    Name = "Bonus Songs 4",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 175,
                    Songs = tierBonusSongs4,
                    IsBonus = true,
                },
                new()
                {
                    Id = new Guid("89904050-9cd4-54f4-adb2-fb038dd006a8"),
                    Name = "Set 8",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 175,
                    Songs = tierSet8,
                },
                new()
                {
                    Id = new Guid("58a12af9-dd41-5d32-9a35-f4a124cc40f8"),
                    Name = "Career Finale",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 200,
                    Songs = tierCareerFinale,
                },
                new()
                {
                    Id = new Guid("a9ac9674-48b2-55c2-b3ed-aec99d7ebfce"),
                    Name = "Boss Songs",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 200,
                    Songs = tierBossSongs,
                },
                new()
                {
                    Id = new Guid("35910a9f-c468-55d9-a92e-6fd64e85ed6b"),
                    Name = "Sidequest Songs",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 250,
                    Songs = tierSidequestSongs,
                    IsBonus = true,
                },
            };

            return new CareerBase(
                new Guid("7e9ba306-2dc8-4dbf-83f4-80325844b1c4"),
                "YARG Setlist Pop",
                "So you wanna be a pop star. Get to it!",
                null,
                true,
                tiers);
        }
    }
}