using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Discord
{
    public enum Result
    {
        Ok = 0,
        ServiceUnavailable = 1,
        InvalidVersion = 2,
        LockFailed = 3,
        InternalError = 4,
        InvalidPayload = 5,
        InvalidCommand = 6,
        InvalidPermissions = 7,
        NotFetched = 8,
        NotFound = 9,
        Conflict = 10,
        InvalidSecret = 11,
        InvalidJoinSecret = 12,
        NoEligibleActivity = 13,
        InvalidInvite = 14,
        NotAuthenticated = 15,
        InvalidAccessToken = 16,
        ApplicationMismatch = 17,
        InvalidDataUrl = 18,
        InvalidBase64 = 19,
        NotFiltered = 20,
        LobbyFull = 21,
        InvalidLobbySecret = 22,
        InvalidFilename = 23,
        InvalidFileSize = 24,
        InvalidEntitlement = 25,
        NotInstalled = 26,
        NotRunning = 27,
        InsufficientBuffer = 28,
        PurchaseCanceled = 29,
        InvalidGuild = 30,
        InvalidEvent = 31,
        InvalidChannel = 32,
        InvalidOrigin = 33,
        RateLimited = 34,
        OAuth2Error = 35,
        SelectChannelTimeout = 36,
        GetGuildTimeout = 37,
        SelectVoiceForceRequired = 38,
        CaptureShortcutAlreadyListening = 39,
        UnauthorizedForAchievement = 40,
        InvalidGiftCode = 41,
        PurchaseError = 42,
        TransactionAborted = 43,
        DrawingInitFailed = 44,
    }

    public enum CreateFlags
    {
        Default = 0,
        NoRequireDiscord = 1,
    }

    public enum LogLevel
    {
        Error = 1,
        Warn,
        Info,
        Debug,
    }

    public enum UserFlag
    {
        Partner = 2,
        HypeSquadEvents = 4,
        HypeSquadHouse1 = 64,
        HypeSquadHouse2 = 128,
        HypeSquadHouse3 = 256,
    }

    public enum PremiumType
    {
        None = 0,
        Tier1 = 1,
        Tier2 = 2,
    }

    public enum ImageType
    {
        User,
    }

    public enum ActivityPartyPrivacy
    {
        Private = 0,
        Public = 1,
    }

    public enum ActivityType
    {
        Playing,
        Streaming,
        Listening,
        Watching,
    }

    public enum ActivityActionType
    {
        Join = 1,
        Spectate,
    }

    public enum ActivitySupportedPlatformFlags
    {
        Desktop = 1,
        Android = 2,
        iOS = 4,
    }

    public enum ActivityJoinRequestReply
    {
        No,
        Yes,
        Ignore,
    }

    public enum Status
    {
        Offline = 0,
        Online = 1,
        Idle = 2,
        DoNotDisturb = 3,
    }

    public enum RelationshipType
    {
        None,
        Friend,
        Blocked,
        PendingIncoming,
        PendingOutgoing,
        Implicit,
    }

    public enum LobbyType
    {
        Private = 1,
        Public,
    }

    public enum LobbySearchComparison
    {
        LessThanOrEqual = -2,
        LessThan,
        Equal,
        GreaterThan,
        GreaterThanOrEqual,
        NotEqual,
    }

    public enum LobbySearchCast
    {
        String = 1,
        Number,
    }

    public enum LobbySearchDistance
    {
        Local,
        Default,
        Extended,
        Global,
    }

    public enum KeyVariant
    {
        Normal,
        Right,
        Left,
    }

    public enum MouseButton
    {
        Left,
        Middle,
        Right,
    }

    public enum EntitlementType
    {
        Purchase = 1,
        PremiumSubscription,
        DeveloperGift,
        TestModePurchase,
        FreePurchase,
        UserGift,
        PremiumPurchase,
    }

    public enum SkuType
    {
        Application = 1,
        DLC,
        Consumable,
        Bundle,
    }

    public enum InputModeType
    {
        VoiceActivity = 0,
        PushToTalk,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct User
    {
        public Int64 Id;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Username;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
        public string Discriminator;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Avatar;

        public bool Bot;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct OAuth2Token
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string AccessToken;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string Scopes;

        public Int64 Expires;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct ImageHandle
    {
        public ImageType Type;

        public Int64 Id;

        public UInt32 Size;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct ImageDimensions
    {
        public UInt32 Width;

        public UInt32 Height;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct ActivityTimestamps
    {
        public Int64 Start;

        public Int64 End;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct ActivityAssets
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string LargeImage;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string LargeText;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string SmallImage;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string SmallText;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct PartySize
    {
        public Int32 CurrentSize;

        public Int32 MaxSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct ActivityParty
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Id;

        public PartySize Size;

        public ActivityPartyPrivacy Privacy;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct ActivitySecrets
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Match;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Join;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Spectate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct Activity
    {
        public ActivityType Type;

        public Int64 ApplicationId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Name;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string State;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Details;

        public ActivityTimestamps Timestamps;

        public ActivityAssets Assets;

        public ActivityParty Party;

        public ActivitySecrets Secrets;

        public bool Instance;

        public UInt32 SupportedPlatforms;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct Presence
    {
        public Status Status;

        public Activity Activity;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct Relationship
    {
        public RelationshipType Type;

        public User User;

        public Presence Presence;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct Lobby
    {
        public Int64 Id;

        public LobbyType Type;

        public Int64 OwnerId;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Secret;

        public UInt32 Capacity;

        public bool Locked;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct ImeUnderline
    {
        public Int32 From;

        public Int32 To;

        public UInt32 Color;

        public UInt32 BackgroundColor;

        public bool Thick;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct Rect
    {
        public Int32 Left;

        public Int32 Top;

        public Int32 Right;

        public Int32 Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct FileStat
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string Filename;

        public UInt64 Size;

        public UInt64 LastModified;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct Entitlement
    {
        public Int64 Id;

        public EntitlementType Type;

        public Int64 SkuId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct SkuPrice
    {
        public UInt32 Amount;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string Currency;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct Sku
    {
        public Int64 Id;

        public SkuType Type;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Name;

        public SkuPrice Price;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct InputMode
    {
        public InputModeType Type;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Shortcut;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct UserAchievement
    {
        public Int64 UserId;

        public Int64 AchievementId;

        public byte PercentComplete;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string UnlockedAt;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct LobbyTransaction
    {
        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIMethods
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result SetTypeMethod(IntPtr methodsPtr, LobbyType type);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result SetOwnerMethod(IntPtr methodsPtr, Int64 ownerId);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result SetCapacityMethod(IntPtr methodsPtr, UInt32 capacity);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result SetMetadataMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string key, [MarshalAs(UnmanagedType.LPStr)]string value);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result DeleteMetadataMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string key);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result SetLockedMethod(IntPtr methodsPtr, bool locked);

            internal SetTypeMethod SetType;

            internal SetOwnerMethod SetOwner;

            internal SetCapacityMethod SetCapacity;

            internal SetMetadataMethod SetMetadata;

            internal DeleteMetadataMethod DeleteMetadata;

            internal SetLockedMethod SetLocked;
        }

        internal IntPtr MethodsPtr;

        internal Object MethodsStructure;

        private FFIMethods Methods
        {
            get
            {
                if (MethodsStructure == null)
                {
                    MethodsStructure = Marshal.PtrToStructure(MethodsPtr, typeof(FFIMethods));
                }
                return (FFIMethods)MethodsStructure;
            }

        }

        public void SetType(LobbyType type)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.SetType(MethodsPtr, type);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void SetOwner(Int64 ownerId)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.SetOwner(MethodsPtr, ownerId);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void SetCapacity(UInt32 capacity)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.SetCapacity(MethodsPtr, capacity);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void SetMetadata(string key, string value)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.SetMetadata(MethodsPtr, key, value);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void DeleteMetadata(string key)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.DeleteMetadata(MethodsPtr, key);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void SetLocked(bool locked)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.SetLocked(MethodsPtr, locked);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct LobbyMemberTransaction
    {
        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIMethods
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result SetMetadataMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string key, [MarshalAs(UnmanagedType.LPStr)]string value);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result DeleteMetadataMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string key);

            internal SetMetadataMethod SetMetadata;

            internal DeleteMetadataMethod DeleteMetadata;
        }

        internal IntPtr MethodsPtr;

        internal Object MethodsStructure;

        private FFIMethods Methods
        {
            get
            {
                if (MethodsStructure == null)
                {
                    MethodsStructure = Marshal.PtrToStructure(MethodsPtr, typeof(FFIMethods));
                }
                return (FFIMethods)MethodsStructure;
            }

        }

        public void SetMetadata(string key, string value)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.SetMetadata(MethodsPtr, key, value);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void DeleteMetadata(string key)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.DeleteMetadata(MethodsPtr, key);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public partial struct LobbySearchQuery
    {
        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIMethods
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result FilterMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string key, LobbySearchComparison comparison, LobbySearchCast cast, [MarshalAs(UnmanagedType.LPStr)]string value);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result SortMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string key, LobbySearchCast cast, [MarshalAs(UnmanagedType.LPStr)]string value);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result LimitMethod(IntPtr methodsPtr, UInt32 limit);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result DistanceMethod(IntPtr methodsPtr, LobbySearchDistance distance);

            internal FilterMethod Filter;

            internal SortMethod Sort;

            internal LimitMethod Limit;

            internal DistanceMethod Distance;
        }

        internal IntPtr MethodsPtr;

        internal Object MethodsStructure;

        private FFIMethods Methods
        {
            get
            {
                if (MethodsStructure == null)
                {
                    MethodsStructure = Marshal.PtrToStructure(MethodsPtr, typeof(FFIMethods));
                }
                return (FFIMethods)MethodsStructure;
            }

        }

        public void Filter(string key, LobbySearchComparison comparison, LobbySearchCast cast, string value)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.Filter(MethodsPtr, key, comparison, cast, value);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void Sort(string key, LobbySearchCast cast, string value)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.Sort(MethodsPtr, key, cast, value);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void Limit(UInt32 limit)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.Limit(MethodsPtr, limit);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }

        public void Distance(LobbySearchDistance distance)
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                var res = Methods.Distance(MethodsPtr, distance);
                if (res != Result.Ok)
                {
                    throw new ResultException(res);
                }
            }
        }
    }

    public partial class ResultException : Exception
    {
        public readonly Result Result;

        public ResultException(Result result) : base(result.ToString())
        {
        }
    }

    public partial class Discord : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIEvents
        {

        }

        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIMethods
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void DestroyHandler(IntPtr MethodsPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result RunCallbacksMethod(IntPtr methodsPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SetLogHookCallback(IntPtr ptr, LogLevel level, [MarshalAs(UnmanagedType.LPStr)]string message);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SetLogHookMethod(IntPtr methodsPtr, LogLevel minLevel, IntPtr callbackData, SetLogHookCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetApplicationManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetUserManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetImageManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetActivityManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetRelationshipManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetLobbyManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetNetworkManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetOverlayManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetStorageManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetStoreManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetVoiceManagerMethod(IntPtr discordPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate IntPtr GetAchievementManagerMethod(IntPtr discordPtr);

            internal DestroyHandler Destroy;

            internal RunCallbacksMethod RunCallbacks;

            internal SetLogHookMethod SetLogHook;

            internal GetApplicationManagerMethod GetApplicationManager;

            internal GetUserManagerMethod GetUserManager;

            internal GetImageManagerMethod GetImageManager;

            internal GetActivityManagerMethod GetActivityManager;

            internal GetRelationshipManagerMethod GetRelationshipManager;

            internal GetLobbyManagerMethod GetLobbyManager;

            internal GetNetworkManagerMethod GetNetworkManager;

            internal GetOverlayManagerMethod GetOverlayManager;

            internal GetStorageManagerMethod GetStorageManager;

            internal GetStoreManagerMethod GetStoreManager;

            internal GetVoiceManagerMethod GetVoiceManager;

            internal GetAchievementManagerMethod GetAchievementManager;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFICreateParams
        {
            internal Int64 ClientId;

            internal UInt64 Flags;

            internal IntPtr Events;

            internal IntPtr EventData;

            internal IntPtr ApplicationEvents;

            internal UInt32 ApplicationVersion;

            internal IntPtr UserEvents;

            internal UInt32 UserVersion;

            internal IntPtr ImageEvents;

            internal UInt32 ImageVersion;

            internal IntPtr ActivityEvents;

            internal UInt32 ActivityVersion;

            internal IntPtr RelationshipEvents;

            internal UInt32 RelationshipVersion;

            internal IntPtr LobbyEvents;

            internal UInt32 LobbyVersion;

            internal IntPtr NetworkEvents;

            internal UInt32 NetworkVersion;

            internal IntPtr OverlayEvents;

            internal UInt32 OverlayVersion;

            internal IntPtr StorageEvents;

            internal UInt32 StorageVersion;

            internal IntPtr StoreEvents;

            internal UInt32 StoreVersion;

            internal IntPtr VoiceEvents;

            internal UInt32 VoiceVersion;

            internal IntPtr AchievementEvents;

            internal UInt32 AchievementVersion;
        }

        [DllImport(Constants.DllName, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        private static extern Result DiscordCreate(UInt32 version, ref FFICreateParams createParams, out IntPtr manager);

        public delegate void SetLogHookHandler(LogLevel level, string message);

        private GCHandle SelfHandle;

        private IntPtr EventsPtr;

        private FFIEvents Events;

        private IntPtr ApplicationEventsPtr;

        private IntPtr UserEventsPtr;

        private UserManager.FFIEvents UserEvents;

        internal UserManager UserManagerInstance;

        private IntPtr ImageEventsPtr;

        private IntPtr ActivityEventsPtr;

        private ActivityManager.FFIEvents ActivityEvents;

        internal ActivityManager ActivityManagerInstance;

        private IntPtr RelationshipEventsPtr;

        private RelationshipManager.FFIEvents RelationshipEvents;

        internal RelationshipManager RelationshipManagerInstance;

        private IntPtr LobbyEventsPtr;

        private IntPtr NetworkEventsPtr;

        private IntPtr OverlayEventsPtr;

        private OverlayManager.FFIEvents OverlayEvents;

        internal OverlayManager OverlayManagerInstance;

        private IntPtr StorageEventsPtr;

        private IntPtr StoreEventsPtr;

        private IntPtr VoiceEventsPtr;

        private IntPtr AchievementEventsPtr;

        private IntPtr MethodsPtr;

        private Object MethodsStructure;

        private FFIMethods Methods
        {
            get
            {
                if (MethodsStructure == null)
                {
                    MethodsStructure = Marshal.PtrToStructure(MethodsPtr, typeof(FFIMethods));
                }
                return (FFIMethods)MethodsStructure;
            }

        }

        private GCHandle? setLogHook;

        public Discord(Int64 clientId, UInt64 flags)
        {
            FFICreateParams createParams;
            createParams.ClientId = clientId;
            createParams.Flags = flags;
            Events = new FFIEvents();
            EventsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(Events));
            createParams.Events = EventsPtr;
            SelfHandle = GCHandle.Alloc(this);
            createParams.EventData = GCHandle.ToIntPtr(SelfHandle);
            createParams.ApplicationEvents = ApplicationEventsPtr;
            createParams.ApplicationVersion = 1;
            UserEvents = new UserManager.FFIEvents();
            UserEventsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(UserEvents));
            createParams.UserEvents = UserEventsPtr;
            createParams.UserVersion = 1;
            createParams.ImageEvents = ImageEventsPtr;
            createParams.ImageVersion = 1;
            ActivityEvents = new ActivityManager.FFIEvents();
            ActivityEventsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(ActivityEvents));
            createParams.ActivityEvents = ActivityEventsPtr;
            createParams.ActivityVersion = 1;
            RelationshipEvents = new RelationshipManager.FFIEvents();
            RelationshipEventsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(RelationshipEvents));
            createParams.RelationshipEvents = RelationshipEventsPtr;
            createParams.RelationshipVersion = 1;
            createParams.LobbyEvents = LobbyEventsPtr;
            createParams.LobbyVersion = 1;
            createParams.NetworkEvents = NetworkEventsPtr;
            createParams.NetworkVersion = 1;
            OverlayEvents = new OverlayManager.FFIEvents();
            OverlayEventsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(OverlayEvents));
            createParams.OverlayEvents = OverlayEventsPtr;
            createParams.OverlayVersion = 2;
            createParams.StorageEvents = StorageEventsPtr;
            createParams.StorageVersion = 1;
            createParams.StoreEvents = StoreEventsPtr;
            createParams.StoreVersion = 1;
            createParams.VoiceEvents = VoiceEventsPtr;
            createParams.VoiceVersion = 1;
            createParams.AchievementEvents = AchievementEventsPtr;
            createParams.AchievementVersion = 1;
            InitEvents(EventsPtr, ref Events);
            var result = DiscordCreate(3, ref createParams, out MethodsPtr);
            if (result != Result.Ok)
            {
                Dispose();
                throw new ResultException(result);
            }
        }

        private void InitEvents(IntPtr eventsPtr, ref FFIEvents events)
        {
            Marshal.StructureToPtr(events, eventsPtr, false);
        }

        public void Dispose()
        {
            if (MethodsPtr != IntPtr.Zero)
            {
                Methods.Destroy(MethodsPtr);
            }
            SelfHandle.Free();
            Marshal.FreeHGlobal(EventsPtr);
            Marshal.FreeHGlobal(ApplicationEventsPtr);
            Marshal.FreeHGlobal(UserEventsPtr);
            Marshal.FreeHGlobal(ImageEventsPtr);
            Marshal.FreeHGlobal(ActivityEventsPtr);
            Marshal.FreeHGlobal(RelationshipEventsPtr);
            Marshal.FreeHGlobal(LobbyEventsPtr);
            Marshal.FreeHGlobal(NetworkEventsPtr);
            Marshal.FreeHGlobal(OverlayEventsPtr);
            Marshal.FreeHGlobal(StorageEventsPtr);
            Marshal.FreeHGlobal(StoreEventsPtr);
            Marshal.FreeHGlobal(VoiceEventsPtr);
            Marshal.FreeHGlobal(AchievementEventsPtr);
            if (setLogHook.HasValue) {
               setLogHook.Value.Free();
            }
        }

        public void RunCallbacks()
        {
            var res = Methods.RunCallbacks(MethodsPtr);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
        }

        [MonoPInvokeCallback]
        private static void SetLogHookCallbackImpl(IntPtr ptr, LogLevel level, string message)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            SetLogHookHandler callback = (SetLogHookHandler)h.Target;
            callback(level, message);
        }

        public void SetLogHook(LogLevel minLevel, SetLogHookHandler callback)
        {
            if (setLogHook.HasValue) {
               setLogHook.Value.Free();
            }
             setLogHook = GCHandle.Alloc(callback);
            Methods.SetLogHook(MethodsPtr, minLevel, GCHandle.ToIntPtr(setLogHook.Value), SetLogHookCallbackImpl);
        }

        public UserManager GetUserManager()
        {
            if (UserManagerInstance == null) {
                UserManagerInstance = new UserManager(
                  Methods.GetUserManager(MethodsPtr),
                  UserEventsPtr,
                  ref UserEvents
                );
            }
            return UserManagerInstance;
        }

        public ActivityManager GetActivityManager()
        {
            if (ActivityManagerInstance == null) {
                ActivityManagerInstance = new ActivityManager(
                  Methods.GetActivityManager(MethodsPtr),
                  ActivityEventsPtr,
                  ref ActivityEvents
                );
            }
            return ActivityManagerInstance;
        }

        public RelationshipManager GetRelationshipManager()
        {
            if (RelationshipManagerInstance == null) {
                RelationshipManagerInstance = new RelationshipManager(
                  Methods.GetRelationshipManager(MethodsPtr),
                  RelationshipEventsPtr,
                  ref RelationshipEvents
                );
            }
            return RelationshipManagerInstance;
        }

        public OverlayManager GetOverlayManager()
        {
            if (OverlayManagerInstance == null) {
                OverlayManagerInstance = new OverlayManager(
                  Methods.GetOverlayManager(MethodsPtr),
                  OverlayEventsPtr,
                  ref OverlayEvents
                );
            }
            return OverlayManagerInstance;
        }

    }

    internal partial class MonoPInvokeCallbackAttribute : Attribute
    {

    }

    
    public partial class UserManager
    {
        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIEvents
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void CurrentUserUpdateHandler(IntPtr ptr);

            internal CurrentUserUpdateHandler OnCurrentUserUpdate;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIMethods
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result GetCurrentUserMethod(IntPtr methodsPtr, ref User currentUser);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void GetUserCallback(IntPtr ptr, Result result, ref User user);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void GetUserMethod(IntPtr methodsPtr, Int64 userId, IntPtr callbackData, GetUserCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result GetCurrentUserPremiumTypeMethod(IntPtr methodsPtr, ref PremiumType premiumType);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result CurrentUserHasFlagMethod(IntPtr methodsPtr, UserFlag flag, ref bool hasFlag);

            internal GetCurrentUserMethod GetCurrentUser;

            internal GetUserMethod GetUser;

            internal GetCurrentUserPremiumTypeMethod GetCurrentUserPremiumType;

            internal CurrentUserHasFlagMethod CurrentUserHasFlag;
        }

        public delegate void GetUserHandler(Result result, ref User user);

        public delegate void CurrentUserUpdateHandler();

        private IntPtr MethodsPtr;

        private Object MethodsStructure;

        private FFIMethods Methods
        {
            get
            {
                if (MethodsStructure == null)
                {
                    MethodsStructure = Marshal.PtrToStructure(MethodsPtr, typeof(FFIMethods));
                }
                return (FFIMethods)MethodsStructure;
            }

        }

        public event CurrentUserUpdateHandler OnCurrentUserUpdate;

        internal UserManager(IntPtr ptr, IntPtr eventsPtr, ref FFIEvents events)
        {
            if (eventsPtr == IntPtr.Zero) {
                throw new ResultException(Result.InternalError);
            }
            InitEvents(eventsPtr, ref events);
            MethodsPtr = ptr;
            if (MethodsPtr == IntPtr.Zero) {
                throw new ResultException(Result.InternalError);
            }
        }

        private void InitEvents(IntPtr eventsPtr, ref FFIEvents events)
        {
            events.OnCurrentUserUpdate = OnCurrentUserUpdateImpl;
            Marshal.StructureToPtr(events, eventsPtr, false);
        }

        public User GetCurrentUser()
        {
            var ret = new User();
            var res = Methods.GetCurrentUser(MethodsPtr, ref ret);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
            return ret;
        }

        [MonoPInvokeCallback]
        private static void GetUserCallbackImpl(IntPtr ptr, Result result, ref User user)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            GetUserHandler callback = (GetUserHandler)h.Target;
            h.Free();
            callback(result, ref user);
        }

        public void GetUser(Int64 userId, GetUserHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.GetUser(MethodsPtr, userId, GCHandle.ToIntPtr(wrapped), GetUserCallbackImpl);
        }

        public PremiumType GetCurrentUserPremiumType()
        {
            var ret = new PremiumType();
            var res = Methods.GetCurrentUserPremiumType(MethodsPtr, ref ret);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
            return ret;
        }

        public bool CurrentUserHasFlag(UserFlag flag)
        {
            var ret = new bool();
            var res = Methods.CurrentUserHasFlag(MethodsPtr, flag, ref ret);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
            return ret;
        }

        [MonoPInvokeCallback]
        private static void OnCurrentUserUpdateImpl(IntPtr ptr)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            Discord d = (Discord)h.Target;
            if (d.UserManagerInstance.OnCurrentUserUpdate != null)
            {
                d.UserManagerInstance.OnCurrentUserUpdate.Invoke();
            }
        }
    }

   

    public partial class ActivityManager
    {
        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIEvents
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ActivityJoinHandler(IntPtr ptr, [MarshalAs(UnmanagedType.LPStr)]string secret);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ActivitySpectateHandler(IntPtr ptr, [MarshalAs(UnmanagedType.LPStr)]string secret);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ActivityJoinRequestHandler(IntPtr ptr, ref User user);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ActivityInviteHandler(IntPtr ptr, ActivityActionType type, ref User user, ref Activity activity);

            internal ActivityJoinHandler OnActivityJoin;

            internal ActivitySpectateHandler OnActivitySpectate;

            internal ActivityJoinRequestHandler OnActivityJoinRequest;

            internal ActivityInviteHandler OnActivityInvite;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIMethods
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result RegisterCommandMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string command);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result RegisterSteamMethod(IntPtr methodsPtr, UInt32 steamId);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void UpdateActivityCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void UpdateActivityMethod(IntPtr methodsPtr, ref Activity activity, IntPtr callbackData, UpdateActivityCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ClearActivityCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ClearActivityMethod(IntPtr methodsPtr, IntPtr callbackData, ClearActivityCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SendRequestReplyCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SendRequestReplyMethod(IntPtr methodsPtr, Int64 userId, ActivityJoinRequestReply reply, IntPtr callbackData, SendRequestReplyCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SendInviteCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SendInviteMethod(IntPtr methodsPtr, Int64 userId, ActivityActionType type, [MarshalAs(UnmanagedType.LPStr)]string content, IntPtr callbackData, SendInviteCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void AcceptInviteCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void AcceptInviteMethod(IntPtr methodsPtr, Int64 userId, IntPtr callbackData, AcceptInviteCallback callback);

            internal RegisterCommandMethod RegisterCommand;

            internal RegisterSteamMethod RegisterSteam;

            internal UpdateActivityMethod UpdateActivity;

            internal ClearActivityMethod ClearActivity;

            internal SendRequestReplyMethod SendRequestReply;

            internal SendInviteMethod SendInvite;

            internal AcceptInviteMethod AcceptInvite;
        }

        public delegate void UpdateActivityHandler(Result result);

        public delegate void ClearActivityHandler(Result result);

        public delegate void SendRequestReplyHandler(Result result);

        public delegate void SendInviteHandler(Result result);

        public delegate void AcceptInviteHandler(Result result);

        public delegate void ActivityJoinHandler(string secret);

        public delegate void ActivitySpectateHandler(string secret);

        public delegate void ActivityJoinRequestHandler(ref User user);

        public delegate void ActivityInviteHandler(ActivityActionType type, ref User user, ref Activity activity);

        private IntPtr MethodsPtr;

        private Object MethodsStructure;

        private FFIMethods Methods
        {
            get
            {
                if (MethodsStructure == null)
                {
                    MethodsStructure = Marshal.PtrToStructure(MethodsPtr, typeof(FFIMethods));
                }
                return (FFIMethods)MethodsStructure;
            }

        }

        public event ActivityJoinHandler OnActivityJoin;

        public event ActivitySpectateHandler OnActivitySpectate;

        public event ActivityJoinRequestHandler OnActivityJoinRequest;

        public event ActivityInviteHandler OnActivityInvite;

        internal ActivityManager(IntPtr ptr, IntPtr eventsPtr, ref FFIEvents events)
        {
            if (eventsPtr == IntPtr.Zero) {
                throw new ResultException(Result.InternalError);
            }
            InitEvents(eventsPtr, ref events);
            MethodsPtr = ptr;
            if (MethodsPtr == IntPtr.Zero) {
                throw new ResultException(Result.InternalError);
            }
        }

        private void InitEvents(IntPtr eventsPtr, ref FFIEvents events)
        {
            events.OnActivityJoin = OnActivityJoinImpl;
            events.OnActivitySpectate = OnActivitySpectateImpl;
            events.OnActivityJoinRequest = OnActivityJoinRequestImpl;
            events.OnActivityInvite = OnActivityInviteImpl;
            Marshal.StructureToPtr(events, eventsPtr, false);
        }

        public void RegisterCommand(string command)
        {
            var res = Methods.RegisterCommand(MethodsPtr, command);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
        }

        public void RegisterSteam(UInt32 steamId)
        {
            var res = Methods.RegisterSteam(MethodsPtr, steamId);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
        }

        [MonoPInvokeCallback]
        private static void UpdateActivityCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            UpdateActivityHandler callback = (UpdateActivityHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void UpdateActivity(Activity activity, UpdateActivityHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.UpdateActivity(MethodsPtr, ref activity, GCHandle.ToIntPtr(wrapped), UpdateActivityCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void ClearActivityCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            ClearActivityHandler callback = (ClearActivityHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void ClearActivity(ClearActivityHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.ClearActivity(MethodsPtr, GCHandle.ToIntPtr(wrapped), ClearActivityCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void SendRequestReplyCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            SendRequestReplyHandler callback = (SendRequestReplyHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void SendRequestReply(Int64 userId, ActivityJoinRequestReply reply, SendRequestReplyHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.SendRequestReply(MethodsPtr, userId, reply, GCHandle.ToIntPtr(wrapped), SendRequestReplyCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void SendInviteCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            SendInviteHandler callback = (SendInviteHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void SendInvite(Int64 userId, ActivityActionType type, string content, SendInviteHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.SendInvite(MethodsPtr, userId, type, content, GCHandle.ToIntPtr(wrapped), SendInviteCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void AcceptInviteCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            AcceptInviteHandler callback = (AcceptInviteHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void AcceptInvite(Int64 userId, AcceptInviteHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.AcceptInvite(MethodsPtr, userId, GCHandle.ToIntPtr(wrapped), AcceptInviteCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void OnActivityJoinImpl(IntPtr ptr, string secret)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            Discord d = (Discord)h.Target;
            if (d.ActivityManagerInstance.OnActivityJoin != null)
            {
                d.ActivityManagerInstance.OnActivityJoin.Invoke(secret);
            }
        }

        [MonoPInvokeCallback]
        private static void OnActivitySpectateImpl(IntPtr ptr, string secret)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            Discord d = (Discord)h.Target;
            if (d.ActivityManagerInstance.OnActivitySpectate != null)
            {
                d.ActivityManagerInstance.OnActivitySpectate.Invoke(secret);
            }
        }

        [MonoPInvokeCallback]
        private static void OnActivityJoinRequestImpl(IntPtr ptr, ref User user)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            Discord d = (Discord)h.Target;
            if (d.ActivityManagerInstance.OnActivityJoinRequest != null)
            {
                d.ActivityManagerInstance.OnActivityJoinRequest.Invoke(ref user);
            }
        }

        [MonoPInvokeCallback]
        private static void OnActivityInviteImpl(IntPtr ptr, ActivityActionType type, ref User user, ref Activity activity)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            Discord d = (Discord)h.Target;
            if (d.ActivityManagerInstance.OnActivityInvite != null)
            {
                d.ActivityManagerInstance.OnActivityInvite.Invoke(type, ref user, ref activity);
            }
        }
    }

    public partial class RelationshipManager
    {
        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIEvents
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void RefreshHandler(IntPtr ptr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void RelationshipUpdateHandler(IntPtr ptr, ref Relationship relationship);

            internal RefreshHandler OnRefresh;

            internal RelationshipUpdateHandler OnRelationshipUpdate;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIMethods
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate bool FilterCallback(IntPtr ptr, ref Relationship relationship);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void FilterMethod(IntPtr methodsPtr, IntPtr callbackData, FilterCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result CountMethod(IntPtr methodsPtr, ref Int32 count);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result GetMethod(IntPtr methodsPtr, Int64 userId, ref Relationship relationship);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result GetAtMethod(IntPtr methodsPtr, UInt32 index, ref Relationship relationship);

            internal FilterMethod Filter;

            internal CountMethod Count;

            internal GetMethod Get;

            internal GetAtMethod GetAt;
        }

        public delegate bool FilterHandler(ref Relationship relationship);

        public delegate void RefreshHandler();

        public delegate void RelationshipUpdateHandler(ref Relationship relationship);

        private IntPtr MethodsPtr;

        private Object MethodsStructure;

        private FFIMethods Methods
        {
            get
            {
                if (MethodsStructure == null)
                {
                    MethodsStructure = Marshal.PtrToStructure(MethodsPtr, typeof(FFIMethods));
                }
                return (FFIMethods)MethodsStructure;
            }

        }

        public event RefreshHandler OnRefresh;

        public event RelationshipUpdateHandler OnRelationshipUpdate;

        internal RelationshipManager(IntPtr ptr, IntPtr eventsPtr, ref FFIEvents events)
        {
            if (eventsPtr == IntPtr.Zero) {
                throw new ResultException(Result.InternalError);
            }
            InitEvents(eventsPtr, ref events);
            MethodsPtr = ptr;
            if (MethodsPtr == IntPtr.Zero) {
                throw new ResultException(Result.InternalError);
            }
        }

        private void InitEvents(IntPtr eventsPtr, ref FFIEvents events)
        {
            events.OnRefresh = OnRefreshImpl;
            events.OnRelationshipUpdate = OnRelationshipUpdateImpl;
            Marshal.StructureToPtr(events, eventsPtr, false);
        }

        [MonoPInvokeCallback]
        private static bool FilterCallbackImpl(IntPtr ptr, ref Relationship relationship)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            FilterHandler callback = (FilterHandler)h.Target;
            return callback(ref relationship);
        }

        public void Filter(FilterHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.Filter(MethodsPtr, GCHandle.ToIntPtr(wrapped), FilterCallbackImpl);
            wrapped.Free();
        }

        public Int32 Count()
        {
            var ret = new Int32();
            var res = Methods.Count(MethodsPtr, ref ret);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
            return ret;
        }

        public Relationship Get(Int64 userId)
        {
            var ret = new Relationship();
            var res = Methods.Get(MethodsPtr, userId, ref ret);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
            return ret;
        }

        public Relationship GetAt(UInt32 index)
        {
            var ret = new Relationship();
            var res = Methods.GetAt(MethodsPtr, index, ref ret);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
            return ret;
        }

        [MonoPInvokeCallback]
        private static void OnRefreshImpl(IntPtr ptr)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            Discord d = (Discord)h.Target;
            if (d.RelationshipManagerInstance.OnRefresh != null)
            {
                d.RelationshipManagerInstance.OnRefresh.Invoke();
            }
        }

        [MonoPInvokeCallback]
        private static void OnRelationshipUpdateImpl(IntPtr ptr, ref Relationship relationship)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            Discord d = (Discord)h.Target;
            if (d.RelationshipManagerInstance.OnRelationshipUpdate != null)
            {
                d.RelationshipManagerInstance.OnRelationshipUpdate.Invoke(ref relationship);
            }
        }
    }
   

    public partial class OverlayManager
    {
        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIEvents
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ToggleHandler(IntPtr ptr, bool locked);

            internal ToggleHandler OnToggle;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal partial struct FFIMethods
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void IsEnabledMethod(IntPtr methodsPtr, ref bool enabled);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void IsLockedMethod(IntPtr methodsPtr, ref bool locked);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SetLockedCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SetLockedMethod(IntPtr methodsPtr, bool locked, IntPtr callbackData, SetLockedCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void OpenActivityInviteCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void OpenActivityInviteMethod(IntPtr methodsPtr, ActivityActionType type, IntPtr callbackData, OpenActivityInviteCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void OpenGuildInviteCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void OpenGuildInviteMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string code, IntPtr callbackData, OpenGuildInviteCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void OpenVoiceSettingsCallback(IntPtr ptr, Result result);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void OpenVoiceSettingsMethod(IntPtr methodsPtr, IntPtr callbackData, OpenVoiceSettingsCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate Result InitDrawingDxgiMethod(IntPtr methodsPtr, IntPtr swapchain, bool useMessageForwarding);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void OnPresentMethod(IntPtr methodsPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ForwardMessageMethod(IntPtr methodsPtr, IntPtr message);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void KeyEventMethod(IntPtr methodsPtr, bool down, [MarshalAs(UnmanagedType.LPStr)]string keyCode, KeyVariant variant);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void CharEventMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string character);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void MouseButtonEventMethod(IntPtr methodsPtr, byte down, Int32 clickCount, MouseButton which, Int32 x, Int32 y);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void MouseMotionEventMethod(IntPtr methodsPtr, Int32 x, Int32 y);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ImeCommitTextMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string text);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ImeSetCompositionMethod(IntPtr methodsPtr, [MarshalAs(UnmanagedType.LPStr)]string text, ref ImeUnderline underlines, Int32 from, Int32 to);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void ImeCancelCompositionMethod(IntPtr methodsPtr);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SetImeCompositionRangeCallbackCallback(IntPtr ptr, Int32 from, Int32 to, ref Rect bounds);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SetImeCompositionRangeCallbackMethod(IntPtr methodsPtr, IntPtr callbackData, SetImeCompositionRangeCallbackCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SetImeSelectionBoundsCallbackCallback(IntPtr ptr, Rect anchor, Rect focus, bool isAnchorFirst);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate void SetImeSelectionBoundsCallbackMethod(IntPtr methodsPtr, IntPtr callbackData, SetImeSelectionBoundsCallbackCallback callback);

            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            internal delegate bool IsPointInsideClickZoneMethod(IntPtr methodsPtr, Int32 x, Int32 y);

            internal IsEnabledMethod IsEnabled;

            internal IsLockedMethod IsLocked;

            internal SetLockedMethod SetLocked;

            internal OpenActivityInviteMethod OpenActivityInvite;

            internal OpenGuildInviteMethod OpenGuildInvite;

            internal OpenVoiceSettingsMethod OpenVoiceSettings;

            internal InitDrawingDxgiMethod InitDrawingDxgi;

            internal OnPresentMethod OnPresent;

            internal ForwardMessageMethod ForwardMessage;

            internal KeyEventMethod KeyEvent;

            internal CharEventMethod CharEvent;

            internal MouseButtonEventMethod MouseButtonEvent;

            internal MouseMotionEventMethod MouseMotionEvent;

            internal ImeCommitTextMethod ImeCommitText;

            internal ImeSetCompositionMethod ImeSetComposition;

            internal ImeCancelCompositionMethod ImeCancelComposition;

            internal SetImeCompositionRangeCallbackMethod SetImeCompositionRangeCallback;

            internal SetImeSelectionBoundsCallbackMethod SetImeSelectionBoundsCallback;

            internal IsPointInsideClickZoneMethod IsPointInsideClickZone;
        }

        public delegate void SetLockedHandler(Result result);

        public delegate void OpenActivityInviteHandler(Result result);

        public delegate void OpenGuildInviteHandler(Result result);

        public delegate void OpenVoiceSettingsHandler(Result result);

        public delegate void SetImeCompositionRangeCallbackHandler(Int32 from, Int32 to, ref Rect bounds);

        public delegate void SetImeSelectionBoundsCallbackHandler(Rect anchor, Rect focus, bool isAnchorFirst);

        public delegate void ToggleHandler(bool locked);

        private IntPtr MethodsPtr;

        private Object MethodsStructure;

        private FFIMethods Methods
        {
            get
            {
                if (MethodsStructure == null)
                {
                    MethodsStructure = Marshal.PtrToStructure(MethodsPtr, typeof(FFIMethods));
                }
                return (FFIMethods)MethodsStructure;
            }

        }

        public event ToggleHandler OnToggle;

        internal OverlayManager(IntPtr ptr, IntPtr eventsPtr, ref FFIEvents events)
        {
            if (eventsPtr == IntPtr.Zero) {
                throw new ResultException(Result.InternalError);
            }
            InitEvents(eventsPtr, ref events);
            MethodsPtr = ptr;
            if (MethodsPtr == IntPtr.Zero) {
                throw new ResultException(Result.InternalError);
            }
        }

        private void InitEvents(IntPtr eventsPtr, ref FFIEvents events)
        {
            events.OnToggle = OnToggleImpl;
            Marshal.StructureToPtr(events, eventsPtr, false);
        }

        public bool IsEnabled()
        {
            var ret = new bool();
            Methods.IsEnabled(MethodsPtr, ref ret);
            return ret;
        }

        public bool IsLocked()
        {
            var ret = new bool();
            Methods.IsLocked(MethodsPtr, ref ret);
            return ret;
        }

        [MonoPInvokeCallback]
        private static void SetLockedCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            SetLockedHandler callback = (SetLockedHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void SetLocked(bool locked, SetLockedHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.SetLocked(MethodsPtr, locked, GCHandle.ToIntPtr(wrapped), SetLockedCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void OpenActivityInviteCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            OpenActivityInviteHandler callback = (OpenActivityInviteHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void OpenActivityInvite(ActivityActionType type, OpenActivityInviteHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.OpenActivityInvite(MethodsPtr, type, GCHandle.ToIntPtr(wrapped), OpenActivityInviteCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void OpenGuildInviteCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            OpenGuildInviteHandler callback = (OpenGuildInviteHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void OpenGuildInvite(string code, OpenGuildInviteHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.OpenGuildInvite(MethodsPtr, code, GCHandle.ToIntPtr(wrapped), OpenGuildInviteCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void OpenVoiceSettingsCallbackImpl(IntPtr ptr, Result result)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            OpenVoiceSettingsHandler callback = (OpenVoiceSettingsHandler)h.Target;
            h.Free();
            callback(result);
        }

        public void OpenVoiceSettings(OpenVoiceSettingsHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.OpenVoiceSettings(MethodsPtr, GCHandle.ToIntPtr(wrapped), OpenVoiceSettingsCallbackImpl);
        }

        public void InitDrawingDxgi(IntPtr swapchain, bool useMessageForwarding)
        {
            var res = Methods.InitDrawingDxgi(MethodsPtr, swapchain, useMessageForwarding);
            if (res != Result.Ok)
            {
                throw new ResultException(res);
            }
        }

        public void OnPresent()
        {
            Methods.OnPresent(MethodsPtr);
        }

        public void ForwardMessage(IntPtr message)
        {
            Methods.ForwardMessage(MethodsPtr, message);
        }

        public void KeyEvent(bool down, string keyCode, KeyVariant variant)
        {
            Methods.KeyEvent(MethodsPtr, down, keyCode, variant);
        }

        public void CharEvent(string character)
        {
            Methods.CharEvent(MethodsPtr, character);
        }

        public void MouseButtonEvent(byte down, Int32 clickCount, MouseButton which, Int32 x, Int32 y)
        {
            Methods.MouseButtonEvent(MethodsPtr, down, clickCount, which, x, y);
        }

        public void MouseMotionEvent(Int32 x, Int32 y)
        {
            Methods.MouseMotionEvent(MethodsPtr, x, y);
        }

        public void ImeCommitText(string text)
        {
            Methods.ImeCommitText(MethodsPtr, text);
        }

        public void ImeSetComposition(string text, ImeUnderline underlines, Int32 from, Int32 to)
        {
            Methods.ImeSetComposition(MethodsPtr, text, ref underlines, from, to);
        }

        public void ImeCancelComposition()
        {
            Methods.ImeCancelComposition(MethodsPtr);
        }

        [MonoPInvokeCallback]
        private static void SetImeCompositionRangeCallbackCallbackImpl(IntPtr ptr, Int32 from, Int32 to, ref Rect bounds)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            SetImeCompositionRangeCallbackHandler callback = (SetImeCompositionRangeCallbackHandler)h.Target;
            h.Free();
            callback(from, to, ref bounds);
        }

        public void SetImeCompositionRangeCallback(SetImeCompositionRangeCallbackHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.SetImeCompositionRangeCallback(MethodsPtr, GCHandle.ToIntPtr(wrapped), SetImeCompositionRangeCallbackCallbackImpl);
        }

        [MonoPInvokeCallback]
        private static void SetImeSelectionBoundsCallbackCallbackImpl(IntPtr ptr, Rect anchor, Rect focus, bool isAnchorFirst)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            SetImeSelectionBoundsCallbackHandler callback = (SetImeSelectionBoundsCallbackHandler)h.Target;
            h.Free();
            callback(anchor, focus, isAnchorFirst);
        }

        public void SetImeSelectionBoundsCallback(SetImeSelectionBoundsCallbackHandler callback)
        {
            GCHandle wrapped = GCHandle.Alloc(callback);
            Methods.SetImeSelectionBoundsCallback(MethodsPtr, GCHandle.ToIntPtr(wrapped), SetImeSelectionBoundsCallbackCallbackImpl);
        }

        public bool IsPointInsideClickZone(Int32 x, Int32 y)
        {
            return Methods.IsPointInsideClickZone(MethodsPtr, x, y);
        }

        [MonoPInvokeCallback]
        private static void OnToggleImpl(IntPtr ptr, bool locked)
        {
            GCHandle h = GCHandle.FromIntPtr(ptr);
            Discord d = (Discord)h.Target;
            if (d.OverlayManagerInstance.OnToggle != null)
            {
                d.OverlayManagerInstance.OnToggle.Invoke(locked);
            }
        }
    }
    }