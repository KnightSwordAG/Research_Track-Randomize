﻿using System.Data.Entity;
using System.Web.Mvc;
using Autofac;

using AutoMapper;
using EKSurvey.Core.Models.Identity;
using EKSurvey.Data;
using EKSurvey.UI.Profiles;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace EKSurvey.UI.Modules
{
    public class ApplicationModule : Module
    {
        private static void GenerateMapperConfiguration(IMapperConfigurationExpression config)
        {
            config.AddProfile<ViewModelProfile>();
            config.ConstructServicesUsing(DependencyResolver.Current.GetService);
        }

        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<MembershipDbContext>()
                .Named<DbContext>("membershipDbContext")
                .SingleInstance();

            builder.Register(c => new UserStore<ApplicationUser>(c.ResolveNamed<DbContext>("membershipDbContext")))
                .As<IUserStore<ApplicationUser>>()
                .InstancePerLifetimeScope();

            builder.Register(c => new MapperConfiguration(GenerateMapperConfiguration))
                .AsSelf()
                .SingleInstance();

            builder.Register(c => c.Resolve<MapperConfiguration>().CreateMapper())
                .As<IMapper>()
                .SingleInstance();

        }
    }
}