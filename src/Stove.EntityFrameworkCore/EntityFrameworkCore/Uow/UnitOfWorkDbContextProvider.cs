using Microsoft.EntityFrameworkCore;

using Stove.Domain.Uow;

namespace Stove.EntityFrameworkCore.Uow
{
	/// <summary>
	///     Implements <see cref="IDbContextProvider{TDbContext}" /> that gets DbContext from
	///     active unit of work.
	/// </summary>
	/// <typeparam name="TDbContext">Type of the DbContext</typeparam>
	public class UnitOfWorkDbContextProvider<TDbContext> : IDbContextProvider<TDbContext>
		where TDbContext : DbContext
	{
		private readonly ICurrentUnitOfWorkProvider _currentUnitOfWorkProvider;

		/// <summary>
		///     Creates a new <see cref="UnitOfWorkDbContextProvider{TDbContext}" />.
		/// </summary>
		/// <param name="currentUnitOfWorkProvider"></param>
		public UnitOfWorkDbContextProvider(ICurrentUnitOfWorkProvider currentUnitOfWorkProvider)
		{
			_currentUnitOfWorkProvider = currentUnitOfWorkProvider;
		}

		public TDbContext GetDbContext()
		{
			return _currentUnitOfWorkProvider.Current.GetDbContext<TDbContext>();
		}
	}
}
