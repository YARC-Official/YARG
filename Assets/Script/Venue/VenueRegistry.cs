using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Settings;

namespace YARG.Venue
{
    public enum VenueAvailabilityState
    {
        Enabled,
        HintOnly,
        Disabled,
    }

    public readonly struct AddressableVenueLocation
    {
        public string PrimaryKey                 { get; }
        public bool   ExcludeFromRandomByDefault { get; }

        public AddressableVenueLocation(string primaryKey, bool excludeFromRandomByDefault)
        {
            PrimaryKey = primaryKey;
            ExcludeFromRandomByDefault = excludeFromRandomByDefault;
        }
    }

    public sealed class VenueDescriptor
    {
        public VenueSelectionInfo Info;
        public string             DisplayName;
        public string             Author;
        public string             Description;
        public string             FilePath;
        public string             PreviewImagePath;
        public Sprite             PreviewSprite;
        public bool               ExcludeFromRandomByDefault;
        public bool               HasMetadata;
    }

    public sealed class VenueBrowserSession : IDisposable
    {
        public IReadOnlyList<VenueDescriptor> Venues { get; }

        private AsyncOperationHandle<IList<VenueMetadata>> _metadataHandle;
        private bool                                       _disposed;

        public VenueBrowserSession(IReadOnlyList<VenueDescriptor> venues,
            AsyncOperationHandle<IList<VenueMetadata>> metadataHandle)
        {
            Venues = venues;
            _metadataHandle = metadataHandle;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_metadataHandle.IsValid())
            {
                Addressables.Release(_metadataHandle);
            }

            _metadataHandle = default;
        }
    }

    public static class VenueRegistry
    {
        // Windows and macOS default to case-insensitive filesystems, Linux does not
        private static readonly StringComparison PATH_COMPARISON =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

        public static bool CanUseRemoteVenues()
        {
            return !GlobalVariables.OfflineMode && SettingsManager.Settings.AllowRemoteContent.Value;
        }

        public static async UniTask<List<AddressableVenueLocation>> GetAddressableVenuesAsync()
        {
            var venues = new List<AddressableVenueLocation>();
            if (!CanUseRemoteVenues())
            {
                return venues;
            }

            await WaitForCatalogAsync();

            var excludedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var noRandomLocations = await LoadVenueLocationsAsync(VenueAddressablesConstants.VenueNoRandomLabel);
            if (noRandomLocations != null)
            {
                foreach (var location in noRandomLocations)
                {
                    if (!string.IsNullOrEmpty(location.PrimaryKey))
                    {
                        excludedKeys.Add(location.PrimaryKey);
                    }
                }
            }

            var venueLocations = await LoadVenueLocationsAsync(VenueAddressablesConstants.VenueLabel);
            if (venueLocations == null)
            {
                return venues;
            }

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var location in venueLocations)
            {
                if (string.IsNullOrEmpty(location.PrimaryKey) || !seenKeys.Add(location.PrimaryKey))
                {
                    continue;
                }

                venues.Add(new AddressableVenueLocation(location.PrimaryKey,
                    excludedKeys.Contains(location.PrimaryKey)));
            }

            return venues;
        }

        public static async UniTask<string> FindAddressableVenueKeyAsync(string hint)
        {
            if (string.IsNullOrWhiteSpace(hint) || !CanUseRemoteVenues())
            {
                return null;
            }

            await WaitForCatalogAsync();

            var locations = await LoadVenueLocationsAsync(VenueAddressablesConstants.VenueLabel);
            if (locations == null)
            {
                return null;
            }

            foreach (var location in locations)
            {
                if (string.Equals(location.PrimaryKey, hint, StringComparison.OrdinalIgnoreCase))
                {
                    return location.PrimaryKey;
                }
            }

            return null;
        }

        public static List<VenueDescriptor> GetLocalYargroundVenues()
        {
            var venues = new List<VenueDescriptor>();

            CollectLocalVenues(VenueLoader.VenueFolder, VenueEntrySource.LocalFile, venues);

            var launcherPath = PathHelper.VenuePath;
            if (!string.IsNullOrEmpty(launcherPath) && Directory.Exists(launcherPath))
            {
                CollectLocalVenues(launcherPath, VenueEntrySource.LauncherFile, venues);
            }

            return venues;
        }

        public static string ResolveLocalFilePath(VenueSelectionInfo info)
        {
            string root = info.Source switch
            {
                VenueEntrySource.LocalFile    => VenueLoader.VenueFolder,
                VenueEntrySource.LauncherFile => PathHelper.VenuePath,
                _                             => null,
            };

            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(info.Identifier))
            {
                return null;
            }

            string fullPath;
            string fullRoot;
            try
            {
                fullPath = Path.GetFullPath(Path.Combine(root, info.Identifier));
                fullRoot = Path.GetFullPath(root);
            }
            catch (Exception)
            {
                return null;
            }

            // Identifiers come from the settings file, so a stale or hand-edited one could point outside the
            // venue folder
            var rootPrefix = fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootPrefix, PATH_COMPARISON))
            {
                return null;
            }

            return File.Exists(fullPath) ? fullPath : null;
        }

        public static async UniTask<VenueBrowserSession> LoadBrowserSessionAsync()
        {
            var venues = new List<VenueDescriptor>();
            AsyncOperationHandle<IList<VenueMetadata>> metadataHandle = default;

            // GetAddressableVenuesAsync waits for the remote catalog itself
            var addressableVenues = new List<AddressableVenueLocation>();
            if (CanUseRemoteVenues())
            {
                addressableVenues = await GetAddressableVenuesAsync();
            }

            if (addressableVenues.Count > 0)
            {
                metadataHandle = await LoadVenueMetadataAsync();
            }

            try
            {
                var metadataByAddress = new Dictionary<string, VenueMetadata>(StringComparer.OrdinalIgnoreCase);
                if (metadataHandle.IsValid() && metadataHandle.Status == AsyncOperationStatus.Succeeded
                    && metadataHandle.Result != null)
                {
                    foreach (var metadata in metadataHandle.Result)
                    {
                        if (metadata == null || string.IsNullOrEmpty(metadata.VenueAddress))
                        {
                            continue;
                        }

                        metadataByAddress[metadata.VenueAddress] = metadata;
                    }
                }

                foreach (var location in addressableVenues)
                {
                    var descriptor = new VenueDescriptor
                    {
                        Info = new VenueSelectionInfo
                        {
                            Source = VenueEntrySource.Addressable,
                            Identifier = location.PrimaryKey,
                        },
                        DisplayName = GetRemoteDisplayName(location.PrimaryKey),
                        ExcludeFromRandomByDefault = location.ExcludeFromRandomByDefault,
                    };

                    if (metadataByAddress.TryGetValue(location.PrimaryKey, out var metadata))
                    {
                        if (!string.IsNullOrWhiteSpace(metadata.DisplayName))
                        {
                            descriptor.DisplayName = metadata.DisplayName.Trim();
                        }

                        descriptor.Author = metadata.Author;
                        descriptor.Description = metadata.Description;
                        descriptor.PreviewSprite = metadata.PreviewSprite;
                        descriptor.ExcludeFromRandomByDefault |= metadata.ExcludeFromRandomByDefault;
                        descriptor.HasMetadata = true;
                    }

                    venues.Add(descriptor);
                }

                venues.AddRange(GetLocalYargroundVenues());

                venues.Sort((a, b) =>
                {
                    int sourceComparison = GetSourceSortOrder(a.Info.Source).CompareTo(GetSourceSortOrder(b.Info.Source));
                    if (sourceComparison != 0)
                    {
                        return sourceComparison;
                    }

                    return StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName);
                });

                return new VenueBrowserSession(venues, metadataHandle);
            }
            catch
            {
                // The caller never receives the session, so it can never release the metadata itself
                if (metadataHandle.IsValid())
                {
                    Addressables.Release(metadataHandle);
                }

                throw;
            }
        }

        public static bool IsHidden(VenueSelectionInfo info)
        {
            return SettingsManager.Settings.HiddenVenues.Contains(info);
        }

        public static bool IsPinned(VenueSelectionInfo info)
        {
            var pinned = SettingsManager.Settings.PinnedVenue;
            return pinned.Source != VenueEntrySource.None && pinned == info;
        }

        public static bool IsIncludedInRandom(VenueSelectionInfo info)
        {
            return SettingsManager.Settings.IncludedVenues.Contains(info);
        }

        public static VenueAvailabilityState GetAvailabilityState(VenueDescriptor venue)
        {
            if (IsHidden(venue.Info))
            {
                return VenueAvailabilityState.Disabled;
            }

            if (!venue.ExcludeFromRandomByDefault || IsIncludedInRandom(venue.Info))
            {
                return VenueAvailabilityState.Enabled;
            }

            return VenueAvailabilityState.HintOnly;
        }

        public static bool IsEligibleForRandomPool(VenueSelectionInfo info, bool excludeFromRandomByDefault)
        {
            return !IsHidden(info) && (!excludeFromRandomByDefault || IsIncludedInRandom(info));
        }

        public static void TogglePin(VenueSelectionInfo info)
        {
            var settings = SettingsManager.Settings;
            if (IsPinned(info))
            {
                settings.PinnedVenue = default;
            }
            else
            {
                settings.PinnedVenue = info;
                settings.HiddenVenues.Remove(info);
            }

            SettingsManager.SaveSettings();
        }

        public static void SetDisabled(VenueSelectionInfo info, bool disabled)
        {
            var settings = SettingsManager.Settings;
            if (disabled)
            {
                if (!settings.HiddenVenues.Contains(info))
                {
                    settings.HiddenVenues.Add(info);
                }

                settings.IncludedVenues.Remove(info);

                if (IsPinned(info))
                {
                    settings.PinnedVenue = default;
                }
            }
            else
            {
                settings.HiddenVenues.Remove(info);
            }

            SettingsManager.SaveSettings();
        }

        public static void CycleAvailability(VenueDescriptor venue)
        {
            var info = venue.Info;

            if (!venue.ExcludeFromRandomByDefault)
            {
                SetDisabled(info, !IsHidden(info));
                return;
            }

            var settings = SettingsManager.Settings;
            switch (GetAvailabilityState(venue))
            {
                case VenueAvailabilityState.HintOnly:
                    settings.IncludedVenues.Add(info);
                    break;
                case VenueAvailabilityState.Enabled:
                    settings.IncludedVenues.Remove(info);
                    settings.HiddenVenues.Add(info);

                    if (IsPinned(info))
                    {
                        settings.PinnedVenue = default;
                    }

                    break;
                case VenueAvailabilityState.Disabled:
                    settings.HiddenVenues.Remove(info);
                    settings.IncludedVenues.Remove(info);
                    break;
            }

            SettingsManager.SaveSettings();
        }

        // Identifiers are relative to their root so the settings survive the venue folder being moved
        public static VenueSelectionInfo GetLocalVenueInfo(VenueEntrySource source, string root, string filePath)
        {
            return new VenueSelectionInfo
            {
                Source = source,
                Identifier = Path.GetRelativePath(root, filePath).Replace('\\', '/'),
            };
        }

        private static async UniTask WaitForCatalogAsync()
        {
            var loader = RemoteCatalogLoader.Instance;
            if (loader != null)
            {
                await loader.WaitForCatalogAsync();
            }
        }

        private static async UniTask<List<IResourceLocation>> LoadVenueLocationsAsync(string label)
        {
            AsyncOperationHandle<IList<IResourceLocation>> handle = default;
            try
            {
                handle = Addressables.LoadResourceLocationsAsync(label);
                await handle.Task;

                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    return null;
                }

                return new List<IResourceLocation>(handle.Result);
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to load venue locations for {label}: {ex.Message}");
                return null;
            }
            finally
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static async UniTask<AsyncOperationHandle<IList<VenueMetadata>>> LoadVenueMetadataAsync()
        {
            AsyncOperationHandle<IList<IResourceLocation>> locationHandle = default;
            AsyncOperationHandle<IList<VenueMetadata>> metadataHandle = default;

            try
            {
                locationHandle = Addressables.LoadResourceLocationsAsync(
                    VenueAddressablesConstants.VenueMetadataLabel, typeof(VenueMetadata));

                await locationHandle.Task;

                if (locationHandle.Status != AsyncOperationStatus.Succeeded ||
                    locationHandle.Result == null ||
                    locationHandle.Result.Count == 0)
                {
                    YargLogger.LogFormatWarning("Failed to load venue metadata: Label {0} not found.",
                        VenueAddressablesConstants.VenueMetadataLabel);
                    return default;
                }

                metadataHandle = Addressables.LoadAssetsAsync<VenueMetadata>(
                    VenueAddressablesConstants.VenueMetadataLabel);
                await metadataHandle.Task;

                if (metadataHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    return metadataHandle;
                }

                if (metadataHandle.OperationException != null)
                {
                    YargLogger.LogFormatWarning("Failed to load venue metadata: {0}",
                        metadataHandle.OperationException.Message);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogFormatWarning("Failed to load venue metadata: {0}", ex.Message);
            }
            finally
            {
                if (locationHandle.IsValid())
                {
                    Addressables.Release(locationHandle);
                }
            }

            if (metadataHandle.IsValid())
            {
                Addressables.Release(metadataHandle);
            }

            return default;
        }

        private static void CollectLocalVenues(string root, VenueEntrySource source, List<VenueDescriptor> venues)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return;
            }

            foreach (var filePath in Directory.EnumerateFiles(root, "*.yarground", PathHelper.SafeSearchOptions))
            {
                var metadata = LocalVenueMetadataReader.ReadMetadata(filePath);
                venues.Add(new VenueDescriptor
                {
                    Info = GetLocalVenueInfo(source, root, filePath),
                    DisplayName = metadata.DisplayName,
                    Author = metadata.Author,
                    Description = metadata.Description,
                    FilePath = filePath,
                    PreviewImagePath = metadata.PreviewImagePath,
                    ExcludeFromRandomByDefault = false,
                    HasMetadata = metadata.HasMetadata,
                });
            }
        }

        private static string GetRemoteDisplayName(string key)
        {
            const string prefix = "Venue/";

            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return key.Substring(prefix.Length);
            }

            return key;
        }

        private static int GetSourceSortOrder(VenueEntrySource source)
        {
            return source == VenueEntrySource.Addressable ? 0 : 1;
        }
    }
}
