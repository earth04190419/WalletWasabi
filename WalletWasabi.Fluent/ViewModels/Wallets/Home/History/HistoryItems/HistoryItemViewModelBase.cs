using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using NBitcoin;
using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Home.History.HistoryItems
{
	public abstract partial class HistoryItemViewModelBase : ViewModelBase
	{
		[AutoNotify] private bool _isFlashing;
		[AutoNotify] private int _orderIndex;
		[AutoNotify] private DateTimeOffset _date;
		[AutoNotify] private string _dateString = "";
		[AutoNotify] private bool _isConfirmed;

		protected HistoryItemViewModelBase(int orderIndex, TransactionSummary transactionSummary)
		{
			OrderIndex = orderIndex;
			Id = transactionSummary.TransactionId;

			this.WhenAnyValue(x => x.IsFlashing)
				.Where(x => x)
				.Subscribe(async _ =>
				{
					await Task.Delay(1260);
					IsFlashing = false;
				});
		}

		public uint256 Id { get; }

		public List<string>? FilteredLabel { get; protected set; }

		public List<string>? Label { get; protected set; }

		public bool IsCoinJoin { get; protected set; }

		public Money? Balance { get; protected set; }

		public Money? OutgoingAmount { get; protected set; }

		public Money? IncomingAmount { get; protected set; }

		public ICommand? ShowDetailsCommand { get; protected set; }

		public virtual void Update(HistoryItemViewModelBase item)
		{
			OrderIndex = item.OrderIndex;
			Date = item.Date;
			DateString = item.DateString;
			IsConfirmed = item.IsConfirmed;
		}

		public bool IsSimilar(HistoryItemViewModelBase item) => Id == item.Id;
	}
}
