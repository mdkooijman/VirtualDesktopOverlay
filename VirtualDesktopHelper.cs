using System;
using Microsoft.Win32;

namespace VirtualDesktopOverlay
{
    public static class VirtualDesktopHelper
    {
        public class DesktopInfo
        {
            public string Name { get; set; } = "Desktop 1";
            public int Index { get; set; } = 1;
        }

        public static DesktopInfo GetCurrentDesktopInfo()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops"))
                {
                    if (key != null)
                    {
                        var currentId = key.GetValue("CurrentVirtualDesktop") as byte[];
                        var allIds = key.GetValue("VirtualDesktopIDs") as byte[];

                        if (allIds != null && allIds.Length > 0)
                        {
                            int desktopCount = allIds.Length / 16;
                            int currentIndex = 1;
                            string currentGuid = "";

                            if (currentId != null && currentId.Length == 16)
                            {
                                currentGuid = ByteArrayToGuid(currentId);

                                for (int i = 0; i < desktopCount; i++)
                                {
                                    bool match = true;
                                    for (int j = 0; j < 16; j++)
                                    {
                                        if (allIds[i * 16 + j] != currentId[j])
                                        {
                                            match = false;
                                            break;
                                        }
                                    }
                                    if (match)
                                    {
                                        currentIndex = i + 1;
                                        break;
                                    }
                                }
                            }

                            string customName = GetDesktopName(currentGuid);

                            return new DesktopInfo
                            {
                                Name = !string.IsNullOrEmpty(customName) ? customName : $"Desktop {currentIndex}",
                                Index = currentIndex
                            };
                        }
                    }
                }
            }
            catch { }

            return new DesktopInfo { Name = "Desktop 1", Index = 1 };
        }

        private static string ByteArrayToGuid(byte[] bytes)
        {
            if (bytes == null || bytes.Length != 16)
                return string.Empty;

            var guid = new Guid(bytes);
            return guid.ToString("B").ToUpper();
        }

        private static string GetDesktopName(string guidString)
        {
            if (string.IsNullOrEmpty(guidString))
                return string.Empty;

            try
            {
                string sessionPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo";
                using (var sessionKey = Registry.CurrentUser.OpenSubKey(sessionPath))
                {
                    if (sessionKey != null)
                    {
                        foreach (string sessionName in sessionKey.GetSubKeyNames())
                        {
                            using (var subKey = sessionKey.OpenSubKey(sessionName))
                            {
                                if (subKey != null)
                                {
                                    using (var vdInfoKey = subKey.OpenSubKey("VirtualDesktopInfo"))
                                    {
                                        if (vdInfoKey != null)
                                        {
                                            using (var desktopsKey = vdInfoKey.OpenSubKey("Desktops"))
                                            {
                                                if (desktopsKey != null)
                                                {
                                                    using (var guidKey = desktopsKey.OpenSubKey(guidString))
                                                    {
                                                        if (guidKey != null)
                                                        {
                                                            var name = guidKey.GetValue("Name") as string;
                                                            if (!string.IsNullOrEmpty(name))
                                                                return name;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                string desktopsPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops\Desktops";
                using (var desktopsKey = Registry.CurrentUser.OpenSubKey(desktopsPath))
                {
                    if (desktopsKey != null)
                    {
                        using (var guidKey = desktopsKey.OpenSubKey(guidString))
                        {
                            if (guidKey != null)
                            {
                                var name = guidKey.GetValue("Name") as string;
                                if (!string.IsNullOrEmpty(name))
                                    return name;
                            }
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }
    }
}

