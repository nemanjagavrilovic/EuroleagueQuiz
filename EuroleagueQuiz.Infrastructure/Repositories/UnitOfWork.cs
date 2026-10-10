using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Infrastructure.Persistence;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly EuroleagueDbContext _db;
    public UnitOfWork(EuroleagueDbContext db) => _db = db;
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
