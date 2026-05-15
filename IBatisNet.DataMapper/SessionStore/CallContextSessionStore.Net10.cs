#if NET10_0_OR_GREATER
using System.Threading;
using System.Collections.Generic;

namespace IBatisNet.DataMapper.SessionStore
{
    /// <summary>
    /// AsyncLocal-backed session store used on .NET 10 where remoting CallContext is unavailable.
    /// </summary>
    public class CallContextSessionStore : AbstractSessionStore
    {
        private static readonly AsyncLocal<Dictionary<string, ISqlMapSession>> LocalSessions = new AsyncLocal<Dictionary<string, ISqlMapSession>>();

        public CallContextSessionStore(string sqlMapperId)
            : base(sqlMapperId)
        {
        }

        public override ISqlMapSession LocalSession
        {
            get
            {
                Dictionary<string, ISqlMapSession> sessions = LocalSessions.Value;
                ISqlMapSession session;
                return sessions != null && sessions.TryGetValue(sessionName, out session) ? session : null;
            }
        }

        public override void Store(ISqlMapSession session)
        {
            Dictionary<string, ISqlMapSession> sessions = LocalSessions.Value;
            if (sessions == null)
            {
                sessions = new Dictionary<string, ISqlMapSession>();
                LocalSessions.Value = sessions;
            }

            sessions[sessionName] = session;
        }

        public override void Dispose()
        {
            Dictionary<string, ISqlMapSession> sessions = LocalSessions.Value;
            if (sessions == null)
            {
                return;
            }

            sessions.Remove(sessionName);
            if (sessions.Count == 0)
            {
                LocalSessions.Value = null;
            }
        }
    }
}
#endif
