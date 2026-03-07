using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;

namespace revit_mcp_plugin.Core
{
    public class ExternalEventManager
    {
        private static ExternalEventManager _instance;
        private readonly Dictionary<string, ExternalEventWrapper> _events = new Dictionary<string, ExternalEventWrapper>();
        private bool _isInitialized;
        private UIApplication _uiApp;
        private ILogger _logger;

        public static ExternalEventManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ExternalEventManager();
                return _instance;
            }
        }

        private ExternalEventManager() { }

        public void Initialize(UIApplication uiApp, ILogger logger)
        {
            _uiApp = uiApp;
            _logger = logger;
            _isInitialized = true;
        }

        public ExternalEvent GetOrCreateEvent(IWaitableExternalEventHandler handler, string key)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("ExternalEventManager has not been initialized.");

            if (_events.TryGetValue(key, out var wrapper) && wrapper.Handler == handler)
                return wrapper.Event;

            var externalEvent = ExternalEvent.Create(handler);
            _events[key] = new ExternalEventWrapper
            {
                Event = externalEvent,
                Handler = handler
            };

            _logger?.Info("Created new ExternalEvent for key: {0}", key);
            return externalEvent;
        }

        public void ClearEvents()
        {
            _events.Clear();
        }

        private class ExternalEventWrapper
        {
            public ExternalEvent Event { get; set; }
            public IWaitableExternalEventHandler Handler { get; set; }
        }
    }
}
