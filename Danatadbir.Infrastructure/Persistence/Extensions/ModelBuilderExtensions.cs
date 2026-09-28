using System.Linq.Expressions;
using Danatadbir.Domain.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace Danatadbir.Infrastructure.Persistence.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplySoftDeleteFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var deletedAt = Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
            var filter = Expression.Lambda(Expression.Equal(deletedAt, Expression.Constant(null)), parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
