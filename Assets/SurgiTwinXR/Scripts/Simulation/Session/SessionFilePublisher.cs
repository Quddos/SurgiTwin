using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurgiTwinXR.Simulation
{
    /// <summary>
    /// Phase 1.1 (Task 4): copies an exported session JSON into the device's shared Downloads folder through
    /// Android MediaStore, so research data can be pulled over ADB on a headset whose signed-in Android user is
    /// not user 0 (adb's shell can read another user's MediaStore with `content read --user N`, but not the
    /// app's private external-files directory).
    ///
    /// Needs no permission (an app may always insert into MediaStore.Downloads on Android 10+). The file stays
    /// on the headset; nothing is uploaded. Only the app that created the entry (and ADB / the user's file
    /// manager) can read it; other apps cannot read non-media files in Downloads on Android 11+.
    ///
    /// <see cref="Initialize"/> must run on the main thread; <see cref="Publish"/> is meant for the export worker.
    /// </summary>
    public static class SessionFilePublisher
    {
        static readonly object Gate = new object();
        static readonly Dictionary<string, string> UriBySession = new Dictionary<string, string>();

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject s_Resolver;
#endif

        /// <summary>Main thread: caches the ContentResolver (a JNI global reference usable from any thread).</summary>
        public static void Initialize()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_Resolver != null)
            {
                return;
            }
            try
            {
                AndroidJavaObject context = UnityEngine.Android.AndroidApplication.currentContext;
                s_Resolver = context.Call<AndroidJavaObject>("getContentResolver");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SurgiTwin] Shared-storage export unavailable: {e.Message}");
            }
#endif
        }

        /// <summary>
        /// Copies <paramref name="sourcePath"/> to Download/&lt;subfolder&gt;/&lt;sessionId&gt;.json. A repeated export of
        /// the same session overwrites the same entry. Returns a description of the destination, or null.
        /// </summary>
        public static string Publish(string sourcePath, string sessionId, string subfolder)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (s_Resolver == null)
            {
                return null;
            }
            lock (Gate)
            {
                bool attached = AndroidJNI.AttachCurrentThread() == 0;
                try
                {
                    string displayName = sessionId + ".json";
                    string relativePath = "Download/" + subfolder + "/";

                    AndroidJavaObject uri;
                    if (UriBySession.TryGetValue(sessionId, out string known))
                    {
                        using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                        {
                            uri = uriClass.CallStatic<AndroidJavaObject>("parse", known);
                        }
                    }
                    else
                    {
                        using (var values = new AndroidJavaObject("android.content.ContentValues"))
                        using (var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads"))
                        using (var collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
                        {
                            values.Call("put", "_display_name", displayName);
                            values.Call("put", "mime_type", "application/json");
                            values.Call("put", "relative_path", relativePath);
                            uri = s_Resolver.Call<AndroidJavaObject>("insert", collection, values);
                        }
                        if (uri == null)
                        {
                            Debug.LogWarning("[SurgiTwin] Shared-storage export failed: MediaStore insert returned null.");
                            return null;
                        }
                        UriBySession[sessionId] = uri.Call<string>("toString");
                    }

                    long copied;
                    using (uri)
                    using (var output = s_Resolver.Call<AndroidJavaObject>("openOutputStream", uri, "wt"))
                    using (var input = new AndroidJavaObject("java.io.FileInputStream", sourcePath))
                    using (var fileUtils = new AndroidJavaClass("android.os.FileUtils"))
                    {
                        copied = fileUtils.CallStatic<long>("copy", input, output);
                        input.Call("close");
                        output.Call("close");
                    }
                    return relativePath + displayName + " (" + UriBySession[sessionId] + ", " + copied + " bytes)";
                }
                catch (Exception e)
                {
                    // The private copy is already complete; only the shared copy failed.
                    UriBySession.Remove(sessionId);
                    Debug.LogWarning($"[SurgiTwin] Shared-storage export failed: {e.Message}");
                    return null;
                }
                finally
                {
                    if (attached)
                    {
                        AndroidJNI.DetachCurrentThread();
                    }
                }
            }
#else
            return null;
#endif
        }
    }
}
