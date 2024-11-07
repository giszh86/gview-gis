﻿using gView.Framework.Core.MapServer;

namespace gView.Server.AppCode.Extensions
{
    static public class MapServiceSettngsExtensions
    {
        static public bool IsRunningOrIdle(this IMapServiceSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            return settings.Status == MapServiceStatus.Running || settings.Status == MapServiceStatus.Idle;
        }

        static public bool IsRunning(this IMapServiceSettings settings)
        {
            if (settings == null)
            {
                return false;
            }

            return settings.Status == MapServiceStatus.Running;
        }
    }
}
