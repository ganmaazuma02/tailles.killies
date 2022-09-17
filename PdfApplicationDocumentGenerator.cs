using System;
using System.Linq;
using Nml.Improve.Me.Dependencies;

namespace Nml.Improve.Me
{
	public class PdfApplicationDocumentGenerator : IApplicationDocumentGenerator
	{
		#region Constant strings

		private const string APPSTATE_PENDING = "PendingApplication";
		private const string APPSTATE_ACTIVATED = "ActivatedApplication";
		private const string APPSTATE_INREVIEW = "InReviewApplication";

		private const string REASONTEXT_ADDRESS = "address";
		private const string REASONTEXT_BANK = "bank";

		private const string REVIEWMESSAGE_FRONTPART = "Your application has been placed in review";
		private const string REVIEWMESSAGE_WITHADDRESS = "pending outstanding address verification for FICA purposes.";
		private const string REVIEWMESSAGE_WITHBANK = "pending outstanding bank account verification.";
		private const string REVIEWMESSAGE_DEFAULT = "because of suspicious account behaviour. Please contact support ASAP.";

		private const string APPLICATION_IDNOTFOUND = "No application found for id";
		private const string APPLICATION_INSTATE = "The application is in state";
		private const string APPLICATION_NOVALIDDOCUMENT = "and no valid document can be generated for it.";

		#endregion

		private readonly IDataContext m_dataContext;
		private readonly IPathProvider m_pathProvider;
		private readonly IViewGenerator m_viewGenerator;
		private readonly IConfiguration m_configuration;
		private readonly ILogger<PdfApplicationDocumentGenerator> m_logger;
		private readonly IPdfGenerator m_pdfGenerator;

		public PdfApplicationDocumentGenerator(
			IDataContext dataContext,
			IPathProvider pathProvider,
			IViewGenerator viewGenerator,
			IConfiguration configuration,
			IPdfGenerator pdfGenerator,
			ILogger<PdfApplicationDocumentGenerator> logger)
		{
			m_dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext)); ;
			m_pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
			m_viewGenerator = viewGenerator ?? throw new ArgumentNullException(nameof(viewGenerator));
			m_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
			m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			m_pdfGenerator = pdfGenerator ?? throw new ArgumentNullException(nameof(pdfGenerator));
		}

		public byte[] Generate(Guid applicationId, string baseUri)
		{
			var _application = m_dataContext.Applications.SingleOrDefault(application => application.Id == applicationId);

			if (_application != null)
			{
				var _html = GenerateHtmlForApplication(_application, baseUri);
				var _pdfOptions = GetDefaultPdfOptions();
				var _pdf = m_pdfGenerator.GenerateFromHtml(_html, _pdfOptions);
				return _pdf.ToBytes();
			}
			else
			{
				m_logger.LogWarning(
					$"{APPLICATION_IDNOTFOUND} '{applicationId}'");
				return null;
			}
		}

		#region Private methods

		private PdfOptions GetDefaultPdfOptions()
		{
			return new PdfOptions
			{
				PageNumbers = PageNumbers.Numeric,
				HeaderOptions = new HeaderOptions
				{
					HeaderRepeat = HeaderRepeat.FirstPageOnly,
					HeaderHtml = PdfConstants.Header
				}
			};
		}

		private string GenerateHtmlForApplication(Application application, string baseUri)
        {
			if (baseUri.EndsWith("/"))
				baseUri = baseUri.Substring(baseUri.Length - 1);

			switch (application.State)
			{
				case ApplicationState.Pending:
					return GenerateHtmlForPendingApplication(application, baseUri);
				case ApplicationState.Activated:
					return GenerateHtmlForActivatedApplication(application, baseUri);
				case ApplicationState.InReview:
					return GenerateHtmlForInReviewApplication(application, baseUri);
				default:
					m_logger.LogWarning(
						$"{APPLICATION_INSTATE} '{application.State}' {APPLICATION_NOVALIDDOCUMENT}");
					return null;
			}
		}

		private string GenerateHtmlForPendingApplication(Application application, string baseUri)
        {
			var _path = m_pathProvider.Get(APPSTATE_PENDING);
			var _pendingApplicationViewModel = new PendingApplicationViewModel
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = $"{application.Person.FirstName} {application.Person.Surname}",
				AppliedOn = application.Date,
				SupportEmail = m_configuration.SupportEmail,
				Signature = m_configuration.Signature
			};

			return m_viewGenerator.GenerateFromPath($"{baseUri}{_path}", _pendingApplicationViewModel);
		}

		private string GenerateHtmlForActivatedApplication(Application application, string baseUri)
        {
			var _path = m_pathProvider.Get(APPSTATE_ACTIVATED);
			var _activatedApplicationViewModel = new ActivatedApplicationViewModel
			{
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = $"{application.Person.FirstName} {application.Person.Surname}",
				LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
				PortfolioFunds = application.Products
					.SelectMany(product => product.Funds),
				PortfolioTotalAmount = application.Products
					.SelectMany(product => product.Funds)
					.Select(fund => (fund.Amount - fund.Fees) * m_configuration.TaxRate)
					.Sum(),
				AppliedOn = application.Date,
				SupportEmail = m_configuration.SupportEmail,
				Signature = m_configuration.Signature
			};

			return m_viewGenerator.GenerateFromPath($"{baseUri}{_path}", _activatedApplicationViewModel);
		}

		private string GenerateHtmlForInReviewApplication(Application application, string baseUri)
        {
			var _path = m_pathProvider.Get(APPSTATE_INREVIEW);
			var _inReviewMessage = GetInReviewMessage(application.CurrentReview.Reason);
			var _inReviewApplicationViewModel = new InReviewApplicationViewModel
            {
				ReferenceNumber = application.ReferenceNumber,
				State = application.State.ToDescription(),
				FullName = $"{application.Person.FirstName} {application.Person.Surname}",
				LegalEntity = application.IsLegalEntity ? application.LegalEntity : null,
				PortfolioFunds = application.Products
					.SelectMany(product => product.Funds),
				PortfolioTotalAmount = application.Products
					.SelectMany(product => product.Funds)
					.Select(fund => (fund.Amount - fund.Fees) * m_configuration.TaxRate)
					.Sum(),
				InReviewMessage = _inReviewMessage,
				InReviewInformation = application.CurrentReview,
				AppliedOn = application.Date,
				SupportEmail = m_configuration.SupportEmail,
				Signature = m_configuration.Signature
			};

			return m_viewGenerator.GenerateFromPath($"{baseUri}{_path}", _inReviewApplicationViewModel);
		}

		private string GetInReviewMessage(string reason)
		{
			var _inReviewMessage = REVIEWMESSAGE_FRONTPART;
			switch (reason)
            {
				case var _reasonText when _reasonText.Contains(REASONTEXT_ADDRESS):
					return $"{_inReviewMessage} {REVIEWMESSAGE_WITHADDRESS}";
				case var _reasonText when _reasonText.Contains(REASONTEXT_BANK):
					return $"{_inReviewMessage} {REVIEWMESSAGE_WITHBANK}";
				default:
					return $"{_inReviewMessage} {REVIEWMESSAGE_DEFAULT}";
			}
		}

		#endregion
	}
}
