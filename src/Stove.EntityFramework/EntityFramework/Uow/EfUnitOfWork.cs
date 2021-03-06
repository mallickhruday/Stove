using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Entity;
using System.Data.Entity.Core.Objects;
using System.Data.Entity.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

using Autofac.Extras.IocManager;

using Stove.Domain.Uow;
using Stove.EntityFramework.Utils;
using Stove.Extensions;

namespace Stove.EntityFramework.Uow
{
    /// <summary>
    ///     Implements Unit of work for Entity Framework.
    /// </summary>
    public class EfUnitOfWork : UnitOfWorkBase, ITransientDependency
    {
        private readonly IDbContextResolver _dbContextResolver;
        private readonly IDbContextTypeMatcher _dbContextTypeMatcher;
        private readonly IEfTransactionStrategy _transactionStrategy;

        /// <summary>
        ///     Creates a new <see cref="EfUnitOfWork" />.
        /// </summary>
        public EfUnitOfWork(
            IConnectionStringResolver connectionStringResolver,
            IDbContextResolver dbContextResolver,
            IEfUnitOfWorkFilterExecuter filterExecuter,
            IUnitOfWorkDefaultOptions defaultOptions,
            IDbContextTypeMatcher dbContextTypeMatcher,
            IEfTransactionStrategy transactionStrategy)
            : base(
                connectionStringResolver,
                defaultOptions,
                filterExecuter)
        {
            _dbContextResolver = dbContextResolver;
            _dbContextTypeMatcher = dbContextTypeMatcher;
            _transactionStrategy = transactionStrategy;

            ActiveDbContexts = new Dictionary<string, DbContext>();
        }

        protected IDictionary<string, DbContext> ActiveDbContexts { get; }

        protected override void BeginUow()
        {
            if (Options.IsTransactional == true)
            {
                _transactionStrategy.InitOptions(Options);
            }
        }

        public override void SaveChanges()
        {
            GetAllActiveDbContexts().ForEach(SaveChangesInDbContext);
        }

        public override async Task SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            foreach (DbContext dbContext in GetAllActiveDbContexts())
            {
                await SaveChangesInDbContextAsync(dbContext, cancellationToken);
            }
        }

        public IReadOnlyList<DbContext> GetAllActiveDbContexts()
        {
            return ActiveDbContexts.Values.ToImmutableList();
        }

        protected override void CompleteUow()
        {
            SaveChanges();

            if (Options.IsTransactional == true)
            {
                _transactionStrategy.Commit();
            }
        }

        protected override async Task CompleteUowAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await SaveChangesAsync(cancellationToken);

            if (Options.IsTransactional == true)
            {
                _transactionStrategy.Commit();
            }
        }

        public virtual TDbContext GetOrCreateDbContext<TDbContext>()
            where TDbContext : DbContext
        {
            Type concreteDbContextType = _dbContextTypeMatcher.GetConcreteType(typeof(TDbContext));

            var connectionStringResolveArgs = new ConnectionStringResolveArgs
            {
                ["DbContextType"] = typeof(TDbContext),
                ["DbContextConcreteType"] = concreteDbContextType
            };

            string connectionString = ResolveConnectionString(connectionStringResolveArgs);
            string dbContextKey = concreteDbContextType.FullName + "#" + connectionString;

            if (!ActiveDbContexts.TryGetValue(dbContextKey, out DbContext dbContext))
            {
                if (Options.IsTransactional == true)
                {
                    dbContext = _transactionStrategy.CreateDbContext<TDbContext>(connectionString, _dbContextResolver);
                }
                else
                {
                    dbContext = _dbContextResolver.Resolve<TDbContext>(connectionString);
                }

                if (Options.Timeout.HasValue && !dbContext.Database.CommandTimeout.HasValue)
                {
                    dbContext.Database.CommandTimeout = Options.Timeout.Value.TotalSeconds.To<int>();
                }

                if (Options.IsLazyLoadEnabled.HasValue)
                {
                    dbContext.Configuration.LazyLoadingEnabled = Options.IsLazyLoadEnabled.Value;
                }

                ((IObjectContextAdapter)dbContext).ObjectContext.ObjectMaterialized += (sender, args) => { ObjectContext_ObjectMaterialized(dbContext, args); };

                FilterExecuter.As<IEfUnitOfWorkFilterExecuter>().ApplyCurrentFilters(this, dbContext);

                ActiveDbContexts[dbContextKey] = dbContext;
            }

            return (TDbContext)dbContext;
        }

        protected override void DisposeUow()
        {
            if (Options.IsTransactional == true)
            {
                _transactionStrategy.Dispose();
            }
            else
            {
                foreach (DbContext activeDbContext in GetAllActiveDbContexts())
                {
                    Release(activeDbContext);
                }
            }

            ActiveDbContexts.Clear();
        }

        protected virtual void SaveChangesInDbContext(DbContext dbContext)
        {
            dbContext.SaveChanges();
        }

        protected virtual async Task SaveChangesInDbContextAsync(DbContext dbContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        protected virtual void Release(DbContext dbContext)
        {
            dbContext.Dispose();
        }

        private void ObjectContext_ObjectMaterialized(DbContext dbContext, ObjectMaterializedEventArgs e)
        {
            Type entityType = ObjectContext.GetObjectType(e.Entity.GetType());

            dbContext.Configuration.AutoDetectChangesEnabled = false;
            EntityState previousState = dbContext.Entry(e.Entity).State;

            DateTimePropertyInfoHelper.NormalizeDatePropertyKinds(e.Entity, entityType);

            dbContext.Entry(e.Entity).State = previousState;
            dbContext.Configuration.AutoDetectChangesEnabled = true;
        }
    }
}
