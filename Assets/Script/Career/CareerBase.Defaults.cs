using System;
using YARG.Core.Song;

namespace YARG.Career
{
    public partial class CareerBase
    {
        public static CareerBase[] Defaults => GetDefaults();

        private static CareerBase[] GetDefaults()
        {
            return new[] {
                YargCareer(),
                FirstCareer()
            };
        }

        private static CareerBase YargCareer()
        {
            CareerSong[] tierOne = new[]
            {
                new CareerSong(HashWrapper.FromString("867E35D2D30F4A32E3AC924F5B90A8F97EA94A75")), // A children's keys song
            };

            CareerSong[] tierTwo = new[]
            {
                new CareerSong(Guid.Parse("94f4125f-c30a-4807-a69f-4f6b507026c8")), // Oh, Krissy Baby
                new CareerSong(HashWrapper.FromString("7C6C6597270D3096E9F12401F3CD35475C99DDD9")), // Another children's keys song
            };

            CareerSong[] tierThree = new[]
            {
                new CareerSong(Guid.Parse("06a92c7e-b1db-4159-9471-7c3bc6505155")), // Duvet Thief
                new CareerSong(Guid.Parse("bfced079-86df-4720-b7ae-c98f3508120b")) // Positively Clark Street
            };

            CareerSong[] tierFour = new[]
            {
                new CareerSong(Guid.Parse("3e7849c4-bac4-4749-bfca-e5ccb349bef9")) // Circles
            };

            CareerTier[] tiers =
            {
                new()
                {
                    Id = new Guid("ad1fc8e8-ec32-494b-9314-d0e25bed71c0"),
                    Name = "Warmup",
                    VenueSize = VenueSize.Starter,
                    VenueHint = "ChillDesk",
                    UnlockType = UnlockType.CompletionCount,
                    UnlockCriteria = 0,
                    Songs = tierOne,
                },
                new()
                {
                    Id = new Guid("f5683a54-5b31-4de3-b506-d22c97941307"),
                    Name = "Building A Name",
                    VenueSize = VenueSize.Small,
                    VenueHint = "Outdoor",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 5,
                    Songs = tierTwo,
                    CustomUnlockText = "You're moving on up...to the east side. To a dee-luxe apartment in the sky.",
                    CompletionBonus = CompletionBonusType.Video,
                    MediaFilename = "rick.webm",
                },
                new ()
                {
                    Id = new Guid("6ebbd5a3-d49c-402d-a488-b72a5b70ed33"),
                    Name = "Early Success",
                    VenueSize = VenueSize.Medium,
                    VenueHint = "Church",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 10,
                    Songs = tierThree,
                    CustomUnlockText = "Well that was a bit underwhelming, wasn't it?"
                },
                new ()
                {
                    Id = new Guid("35c6fb06-2c2e-4555-96d3-323481a72ca8"),
                    Name = "How'd That Happen?",
                    VenueSize = VenueSize.Large,
                    VenueHint = "Festival",
                    UnlockType = UnlockType.StarCount,
                    UnlockCriteria = 10,
                    Songs = tierFour,
                    CustomUnlockText = "Fame and fortune are yours!"
                }
            };

            return new CareerBase(new Guid("03e2d177-21cc-48f7-82b6-728cdb71f8bb"), "YARG Career", "A test career with YARG setlist songs", "toodumb.png", tiers);
        }

        private static CareerBase FirstCareer()
        {
            CareerSong[] songs = new[]
            {
                new CareerSong(HashWrapper.FromString("867E35D2D30F4A32E3AC924F5B90A8F97EA94A75"))
                {
                    Id = Guid.Parse("4ab2cc94-8313-471b-9650-ddc54baad317")
                },
                new CareerSong(HashWrapper.FromString("7C6C6597270D3096E9F12401F3CD35475C99DDD9"))
                {
                    Id = Guid.Parse("c349565f-0db4-41c5-8ca6-c5a80be096fd")
                },
                new CareerSong(HashWrapper.FromString("0CBB935F211B0C6C349852416D8A420DB5A0F10B")),
                new CareerSong(new SongTuple
                    {
                        Artist = "Groundlift",
                        Title = "Bottleneck",
                        Charter = "YARC",
                        Source = "yarg"
                    })
                {
                    Id = Guid.Parse("05895391-01f9-4569-ae50-75da541f27f2")
                }
            };

            CareerTier[] tiers =
            {
                new()
                {
                    Id = new Guid("3a31b249-d76d-4692-a169-38b3291f0cb5"),
                    Name = "First Tier",
                    Description = "If you want to get good, you better practice!",
                    VenueSize = VenueSize.Starter,
                    VenueHint = "ChillDesk",
                    UnlockType = UnlockType.CompletionCount,
                    UnlockCriteria = 0,
                    Songs = songs //new []
                    // {
                    //     // new CareerSong
                    //     // {
                    //     //     Identifier = CareerSongIdentifier.SongHash,
                    //     //     SongHash = HashWrapper.FromString("0CBB935F211B0C6C349852416D8A420DB5A0F10B"),
                    //     //     Description = "All of a Sudden, from the YARG Setlist",
                    //     // },
                    //     // new CareerSong
                    //     // {
                    //     //     Identifier = CareerSongIdentifier.SongTuple,
                    //     //     SongTuple = new SongTuple
                    //     //     {
                    //     //         Artist = "Groundlift",
                    //     //         Title = "Bottleneck",
                    //     //         Charter = "YARC",
                    //     //         Source = "yarg"
                    //     //     }
                    //     // }
                    // }
                }
            };

            return new CareerBase(new Guid("b762b4d9-e798-46bb-a190-bb7e8fa653c2"),
                "Humble Beginnings",
                "Yargina George has always wanted to perform on stage and go on tour.\n\nArmed with their newly produced #1 hit single, Yargina and her bandmates Anton, Randrew, and G headline the prestigious Two Weeks Festival.\n\nWill their success last or will those pesky accusations of \"nepo baby\" and \"payola\" drag them down?",
                "rick.webp",
                "yarg",
                tiers);
        }
    }
}