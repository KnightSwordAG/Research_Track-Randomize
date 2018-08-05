﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;

using EKSurvey.Core.Models.DataTransfer;
using EKSurvey.Core.Models.Entities;
using EKSurvey.Core.Models.Enums;
using EKSurvey.Core.Services.Exceptions;

namespace EKSurvey.Core.Services
{
    public class SurveyManager : ISurveyManager
    {
        private readonly DbContext _dbContext;
        private readonly IMapper _mapper;

        public SurveyManager(DbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        protected DbSet<Survey> Surveys => _dbContext.Set<Survey>();
        protected DbSet<Section> Sections => _dbContext.Set<Section>();
        protected DbSet<Page> Pages => _dbContext.Set<Page>();
        protected DbSet<Test> Tests => _dbContext.Set<Test>();

        private Action<IMappingOperationOptions> Opt(string userId) => o =>
        {
            o.Items.Add("dbContext", _dbContext);
            o.Items.Add("userId", userId);
        };

        private static UserSection SelectSection(UserSectionGroup sectionGroup)
        {
            var rng = new Random();
            var selectionIndex = 0;
            switch (sectionGroup.SelectorType.GetValueOrDefault(SelectorType.Random))
            {
                case SelectorType.Random:
                    selectionIndex = rng.Next(0, sectionGroup.Count);
                    break;
                case SelectorType.ResponseStandardDeviation:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sectionGroup.SelectorType), sectionGroup.SelectorType, null);
            }

            return sectionGroup[selectionIndex];
        }
        private void MakeActiveSection(IUserSection userSection)
        {
            var sectionId = default(int);
            switch (userSection)
            {
                case UserSection _:
                    sectionId = userSection.Id;
                    break;
                case UserSectionGroup group:
                    var selectedSection = SelectSection(group);
                    sectionId = selectedSection.Id;
                    break;
            }

            var section = Sections.Find(sectionId) ?? throw new SectionNotFoundException(sectionId);
            var sectionMarker = new TestSectionMarker
            {
                TestId = userSection.TestId,
                SectionId = userSection.Id,
                Started = DateTime.UtcNow
            };
            section.TestSectionMarkers.Add(sectionMarker);
            _dbContext.SaveChanges();
            _dbContext.Entry(section).Reload();
        }
        private async Task MakeActiveSectionAsync(IUserSection userSection)
        {
            var sectionId = default(int);
            switch (userSection)
            {
                case UserSection _:
                    sectionId = userSection.Id;
                    break;
                case UserSectionGroup group:
                    var selectedSection = SelectSection(group);
                    sectionId = selectedSection.Id;
                    break;
            }

            var section = await Sections.FindAsync(sectionId) ?? throw new SectionNotFoundException(sectionId);
            var sectionMarker = new TestSectionMarker
            {
                TestId = userSection.TestId,
                SectionId = userSection.Id,
                Started = DateTime.UtcNow
            };
            section.TestSectionMarkers.Add(sectionMarker);
            await _dbContext.SaveChangesAsync();
            await _dbContext.Entry(section).ReloadAsync();
        }

        public IQueryable<Survey> GetActiveSurveys()
        {
            var surveys =
                from s in Surveys.Include(s => s.Tests)
                where s.IsActive &&
                      !s.Deleted.HasValue
                select s;

            return surveys;
        }

        public Task<IQueryable<Survey>> GetActiveSurveysAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var surveys =
                from s in Surveys.Include(s => s.Tests)
                where s.IsActive &&
                      !s.Deleted.HasValue
                select s;

            return Task.FromResult(surveys);
        }

        public ICollection<UserSurvey> GetUserSurveys(string userId, bool includeCompleted = false)
        {
            IEnumerable<UserSurvey> results;

            if (includeCompleted)
            {
                results = _mapper.Map<IEnumerable<UserSurvey>>(GetActiveSurveys(), Opt(userId));
                return new HashSet<UserSurvey>(results);
            }

            var surveys =
                from s in GetActiveSurveys()
                where !s.Tests.Any(st => st.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase) && st.Completed.HasValue)
                select s;

            results = _mapper.Map<IEnumerable<UserSurvey>>(surveys, Opt(userId));
            return new HashSet<UserSurvey>(results);
        }

        public async Task<ICollection<UserSurvey>> GetUserSurveysAsync(string userId, bool includeCompleted = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            IEnumerable<UserSurvey> results;

            if (includeCompleted)
            {
                results = _mapper.Map<IEnumerable<UserSurvey>>(await GetActiveSurveysAsync(cancellationToken), Opt(userId));
                return new HashSet<UserSurvey>(results);
            }

            var surveys =
                from s in await GetActiveSurveysAsync(cancellationToken)
                where !s.Tests.Any(st => st.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase) && st.Completed.HasValue)
                select s;

            results = _mapper.Map<IEnumerable<UserSurvey>>(surveys, Opt(userId));
            return new HashSet<UserSurvey>(results);
        }

        public ICollection<IUserSection> GetUserSections(string userId, int surveyId)
        {
            var survey = Surveys.Find(surveyId) ?? throw new SurveyNotFoundException(surveyId);

            var sectionStacks =
                from s in survey.Sections
                group s by s.Order
                into st
                select new { Order = st.Key, Stack = st.ToList() };

            var userSections = sectionStacks
                .OrderBy(ss => ss.Order)
                .Select(ss => ss.Stack.Count == 1
                    ? (IUserSection)_mapper.Map<UserSection>(ss.Stack.First(), Opt(userId))
                    : _mapper.Map<UserSectionGroup>(ss.Stack, Opt(userId)));

            return new HashSet<IUserSection>(userSections.OrderBy(i => i.Order));
        }

        public async Task<ICollection<IUserSection>> GetUserSectionsAsync(string userId, int surveyId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var survey = await Surveys.FindAsync(cancellationToken, surveyId) ?? throw new SurveyNotFoundException(surveyId);

            var sectionStacks =
                from s in survey.Sections
                group s by s.Order
                into st
                select new {Order = st.Key, Stack = st.ToList()};

            var userSections = sectionStacks
                .OrderBy(ss => ss.Order)
                .Select(ss => ss.Stack.Count == 1 
                              ? (IUserSection) _mapper.Map<UserSection>(ss.Stack.First(), Opt(userId))
                              : _mapper.Map<UserSectionGroup>(ss.Stack, Opt(userId)));

            return new HashSet<IUserSection>(userSections.OrderBy(i => i.Order));
        }

        private void ThrowIfSurveyDoesNotExist(int surveyId)
        {
            if (Surveys.Find(surveyId) == null)
                throw new SurveyNotFoundException(surveyId);
        }

        public IUserSection GetCurrentUserSection(string userId, int surveyId)
        {
            ThrowIfSurveyDoesNotExist(surveyId);
            var sections = GetUserSections(userId, surveyId);
            var activeSection = sections.FirstOrDefault(s => s.Started.HasValue && !s.Completed.HasValue);

            if (activeSection != null)
                return activeSection;

            var availableSections =
                from s in sections
                where !s.Started.HasValue
                orderby s.Order
                select s;

            activeSection = availableSections.FirstOrDefault();

            MakeActiveSection(activeSection);

            return activeSection;
        }

        public async Task<IUserSection> GetCurrentUserSectionAsync(string userId, int surveyId, CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfSurveyDoesNotExist(surveyId);
            var sections = await GetUserSectionsAsync(userId, surveyId, cancellationToken);
            var activeSection = sections.FirstOrDefault(s => s.Started.HasValue && !s.Completed.HasValue);

            if (activeSection != null)
                return activeSection;

            var availableSections =
                from s in sections
                where !s.Started.HasValue
                orderby s.Order
                select s;

            activeSection = availableSections.FirstOrDefault();

            await MakeActiveSectionAsync(activeSection);

            return activeSection;
        }

        public ICollection<UserPage> GetUserPages(string userId, int sectionId)
        {
            var section = Sections.Find(sectionId) ?? throw new SectionNotFoundException(sectionId);
            var results = _mapper.Map<ICollection<UserPage>>(section.Pages, Opt(userId));
            return new HashSet<UserPage>(results.OrderBy(i => i.Page.Order));
        }

        public async Task<ICollection<UserPage>> GetUserPagesAsync(string userId, int sectionId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var section = await Sections.FindAsync(cancellationToken, sectionId) ?? throw new SectionNotFoundException(sectionId);
            var results = _mapper.Map<ICollection<UserPage>>(section.Pages, Opt(userId));
            return new HashSet<UserPage>(results.OrderBy(i => i.Page.Order));
        }

        public UserPage GetCurrentUserPage(string userId, int surveyId)
        {
            var activeSection = GetCurrentUserSection(userId, surveyId);
            var pages = GetUserPages(userId, activeSection.Id);

            return pages.OrderBy(p => p.Page.Order).FirstOrDefault(p => !p.Responded.HasValue);
        }

        public async Task<UserPage> GetCurrentUserPageAsync(string userId, int surveyId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var activeSection = await GetCurrentUserSectionAsync(userId, surveyId, cancellationToken);
            var pages = await GetUserPagesAsync(userId, activeSection.Id, cancellationToken);

            return pages.OrderBy(p => p.Page.Order).FirstOrDefault(p => !p.Responded.HasValue);
        }

        public ICollection<UserResponse> GetSectionResponses(string userId, int id)
        {
            var section = Sections.Find(id) ?? throw new SectionNotFoundException(id);
            var test = Tests.Find(userId, section.SurveyId) ?? throw new TestNotFoundException(userId, id);

            var sectionResponses =
                from p in section.Pages
                from r in p.TestResponses
                where r.TestId == test.Id
                orderby r.Page.Order
                select r;

            var responses = _mapper.Map<ICollection<UserResponse>>(sectionResponses, Opt(userId));

            return responses;
        }

        public async Task<ICollection<UserResponse>> GetSectionResponsesAsync(string userId, int id, CancellationToken cancellationToken = default(CancellationToken))
        {
            var section = await Sections.FindAsync(cancellationToken, id) ?? throw new SectionNotFoundException(id);
            var test = await Tests.FindAsync(cancellationToken, userId, section.SurveyId) ?? throw new TestNotFoundException(userId, id);

            var sectionResponses =
                from p in section.Pages
                from r in p.TestResponses
                where r.TestId == test.Id
                orderby r.Page.Order
                select r;

            var responses = _mapper.Map<ICollection<UserResponse>>(sectionResponses, Opt(userId));

            return responses;
        }

        public UserSurvey GetUserSurvey(string userId, int surveyId)
        {
            var survey = Surveys.Find(surveyId) ?? throw new SurveyNotFoundException(surveyId);
            var result = _mapper.Map<UserSurvey>(survey, Opt(userId));
            return result;
        }

        public async Task<UserSurvey> GetUserSurveyAsync(string userId, int surveyId, CancellationToken cancellationToken = default(CancellationToken))
        {
            var survey = await Surveys.FindAsync(cancellationToken, surveyId) ?? throw new SurveyNotFoundException(surveyId);
            var result = _mapper.Map<UserSurvey>(survey, Opt(userId));
            return result;
        }
    }
}
