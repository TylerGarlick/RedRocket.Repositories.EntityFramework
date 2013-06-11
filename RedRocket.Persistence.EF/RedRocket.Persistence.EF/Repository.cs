﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Linq.Expressions;
using System.Transactions;
using FlitBit.IoC;
using RedRocket.Persistence.Common;
using RedRocket.Persistence.EF.ContextFactories;
using RedRocket.Persistence.EF.Validation;
using RedRocket.Utilities.Core.Validation;

namespace RedRocket.Persistence.EF
{
    public class Repository<T> : IRepository<T> where T : class, new()
    {
        public readonly DbContext Db;

        public Repository()
            : this(null)
        { }

        public Repository(DbContext dbContext)
        {
            if (dbContext == null)
            {
                var dbContextFactory = Create.New<IDbContextFactory>();
                Db = dbContextFactory.GetDbContext(new T());
            }
            else
                Db = dbContext;
        }

        public virtual IQueryable<T> All()
        {
            return Db.Set<T>().AsNoTracking();
        }

        public virtual IQueryable<T> Query(Func<T, bool> predicate)
        {
            return All().Where(predicate).AsQueryable();
        }

        public virtual T FindWithKey(Expression<Func<T, bool>> predicate)
        {
            return All().SingleOrDefault(predicate);
        }

        public virtual T Add(T entity)
        {
            var entityValidationResult = GetValidationErrors(entity);
            if (entityValidationResult.IsValid)
            {
                using (var transaction = new TransactionScope())
                {
                    Db.Set<T>().Add(entity);
                    Db.SaveChanges();
                    transaction.Complete();
                    ChangeEntityState(entity, EntityState.Detached);
                    return entity;
                }
            }

            throw new EntityValidationExeption(entityValidationResult);
        }

        public virtual T Update(T entity)
        {
            var entityValidationResult = GetValidationErrors(entity);
            if (entityValidationResult.IsValid)
            {
                using (var transaction = new TransactionScope())
                {
                    entity = Db.Set<T>().Attach(entity);
                    ChangeEntityState(entity, EntityState.Modified);
                    Db.SaveChanges();
                    transaction.Complete();
                    ChangeEntityState(entity, EntityState.Detached);
                    return entity;
                }
            }

            throw new EntityValidationExeption(entityValidationResult);
        }

        public void Delete(T entity)
        {
            using (var transaction = new TransactionScope())
            {
                Db.Set<T>().Attach(entity);
                ChangeEntityState(entity, EntityState.Deleted);
                Db.SaveChanges();
                transaction.Complete();
            }
        }

        public IEnumerable<ObjectValidationError> Validate(T entity)
        {
            var entityValidationResult = GetValidationErrors(entity);
            return entityValidationResult.ValidationErrors.Select(validationError => new ObjectValidationError()
                                                                                         {
                                                                                             Message = validationError.ErrorMessage,
                                                                                             PropertyName = validationError.PropertyName
                                                                                         });
        }

        void ChangeEntityState(T entity, EntityState state)
        {
            var entityMeta = Db.Entry(entity);
            entityMeta.State = state;
        }

        DbEntityValidationResult GetValidationErrors(T entity)
        {
            return Db.Entry(entity).GetValidationResult();
        }
    }
}
