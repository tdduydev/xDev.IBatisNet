#if NET10_0_OR_GREATER
using System;
using System.Threading;
using BclIsolationLevel = System.Transactions.IsolationLevel;
using BclTransaction = System.Transactions.Transaction;
using BclTransactionOptions = System.Transactions.TransactionOptions;
using BclTransactionScope = System.Transactions.TransactionScope;
using BclTransactionScopeOption = System.Transactions.TransactionScopeOption;

namespace IBatisNet.Common.Transaction
{
    /// <summary>
    /// .NET 10 transaction scope adapter that preserves the legacy iBATIS.NET API.
    /// </summary>
    public class TransactionScope : IDisposable
    {
        private static readonly AsyncLocal<int> ScopeCount = new AsyncLocal<int>();
        private readonly BclTransactionScope _scope;
        private bool _closed;
        private bool _completed;

        public TransactionScope()
            : this(TransactionScopeOptions.Required, DefaultOptions())
        {
        }

        public TransactionScope(TransactionScopeOptions txScopeOptions)
            : this(txScopeOptions, DefaultOptions())
        {
        }

        public TransactionScope(TransactionScopeOptions txScopeOptions, TransactionOptions options)
        {
            BclTransactionOptions bclOptions = new BclTransactionOptions
            {
                IsolationLevel = ToBclIsolationLevel(options.IsolationLevel),
                Timeout = options.TimeOut
            };

            _scope = new BclTransactionScope(ToBclScopeOption(txScopeOptions), bclOptions, System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
            ScopeCount.Value = ScopeCount.Value + 1;
        }

        public int TransactionScopeCount
        {
            get { return ScopeCount.Value; }
            set { ScopeCount.Value = value; }
        }

        public static bool IsInTransaction
        {
            get { return BclTransaction.Current != null; }
        }

        public bool IsVoteCommit
        {
            get { return _completed; }
        }

        public void Complete()
        {
            if (!_closed && !_completed)
            {
                _scope.Complete();
                _completed = true;
            }
        }

        public void Close()
        {
            if (_closed)
            {
                return;
            }

            ScopeCount.Value = Math.Max(0, ScopeCount.Value - 1);
            _scope.Dispose();
            _closed = true;
        }

        public void Dispose()
        {
            Close();
        }

        private static TransactionOptions DefaultOptions()
        {
            return new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted,
                TimeOut = new TimeSpan(0, 0, 15)
            };
        }

        private static BclTransactionScopeOption ToBclScopeOption(TransactionScopeOptions options)
        {
            switch (options)
            {
                case TransactionScopeOptions.Required:
                    return BclTransactionScopeOption.Required;
                case TransactionScopeOptions.RequiresNew:
                    return BclTransactionScopeOption.RequiresNew;
                case TransactionScopeOptions.NotSupported:
                    return BclTransactionScopeOption.Suppress;
                case TransactionScopeOptions.Supported:
                    return BclTransaction.Current == null
                        ? BclTransactionScopeOption.Suppress
                        : BclTransactionScopeOption.Required;
                case TransactionScopeOptions.Mandatory:
                    if (BclTransaction.Current == null)
                    {
                        throw new InvalidOperationException("A transaction is required for TransactionScopeOptions.Mandatory.");
                    }
                    return BclTransactionScopeOption.Required;
                default:
                    return BclTransactionScopeOption.Required;
            }
        }

        private static BclIsolationLevel ToBclIsolationLevel(IsolationLevel isolationLevel)
        {
            switch (isolationLevel)
            {
                case IsolationLevel.Serializable:
                    return BclIsolationLevel.Serializable;
                case IsolationLevel.RepeatableRead:
                    return BclIsolationLevel.RepeatableRead;
                case IsolationLevel.ReadCommitted:
                    return BclIsolationLevel.ReadCommitted;
                case IsolationLevel.ReadUncommitted:
                    return BclIsolationLevel.ReadUncommitted;
                case IsolationLevel.Unspecified:
                    return BclIsolationLevel.Unspecified;
                default:
                    return BclIsolationLevel.ReadCommitted;
            }
        }
    }
}
#endif
