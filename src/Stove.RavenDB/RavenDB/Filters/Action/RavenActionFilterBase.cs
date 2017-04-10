﻿using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

using Stove.Domain.Entities;
using Stove.Domain.Uow;
using Stove.Reflection;
using Stove.Runtime.Session;

namespace Stove.RavenDB.Filters.Action
{
    public abstract class RavenActionFilterBase
    {
        protected RavenActionFilterBase()
        {
            StoveSession = NullStoveSession.Instance;
            GuidGenerator = SequentialGuidGenerator.Instance;
        }

        public IStoveSession StoveSession { get; set; }

        public ICurrentUnitOfWorkProvider CurrentUnitOfWorkProvider { get; set; }

        public IGuidGenerator GuidGenerator { get; set; }

        protected virtual long? GetAuditUserId()
        {
            if (StoveSession.UserId.HasValue && CurrentUnitOfWorkProvider?.Current != null)
            {
                return StoveSession.UserId;
            }

            return null;
        }

        protected virtual void CheckAndSetId(object entityAsObj)
        {
            var entity = entityAsObj as IEntity<Guid>;
            if (entity != null && entity.Id == Guid.Empty)
            {
                Type entityType = entityAsObj.GetType();
                PropertyInfo idProperty = entityType.GetProperty("Id");
                var dbGeneratedAttr = ReflectionHelper.GetSingleAttributeOrDefault<DatabaseGeneratedAttribute>(idProperty);
                if (dbGeneratedAttr == null || dbGeneratedAttr.DatabaseGeneratedOption == DatabaseGeneratedOption.None)
                {
                    entity.Id = GuidGenerator.Create();
                }
            }
        }
    }
}