﻿namespace gView.Server.AppCode
{
    class MapServiceAlias : MapService
    {
        private readonly string _serviceName = string.Empty;

        //public MapServiceAlias(string alias, MapServiceType type, string serviceName)
        //    : base(alias, type)
        //{
        //    _serviceName = serviceName;
        //}

        public string ServiceName
        {
            get { return _serviceName; }
        }
    }
}
