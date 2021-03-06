using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNet.Http.Authentication;
using Microsoft.Data.Entity;
using MvcTemplate.Components.Security;
using MvcTemplate.Data.Core;
using MvcTemplate.Objects;
using MvcTemplate.Services;
using MvcTemplate.Tests.Data;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using Xunit;

namespace MvcTemplate.Tests.Unit.Services
{
    public class AccountServiceTests : IDisposable
    {
        private IAuthorizationProvider authorizationProvider;
        private AccountService service;
        private TestingContext context;
        private Account account;
        private IHasher hasher;

        public AccountServiceTests()
        {
            context = new TestingContext();
            hasher = Substitute.For<IHasher>();
            authorizationProvider = Substitute.For<IAuthorizationProvider>();
            hasher.HashPassword(Arg.Any<String>()).Returns(info => info.Arg<String>() + "Hashed");

            context.DropData();
            SetUpData();

            service = new AccountService(new UnitOfWork(context), hasher, authorizationProvider);
            service.CurrentAccountId = account.Id;
        }
        public void Dispose()
        {
            service.Dispose();
            context.Dispose();
        }

        #region Method: Get<TView>(String id)

        [Fact]
        public void Get_ReturnsViewById()
        {
            AccountView actual = service.Get<AccountView>(account.Id);
            AccountView expected = Mapper.Map<AccountView>(account);

            Assert.Equal(expected.CreationDate, actual.CreationDate);
            Assert.Equal(expected.RoleTitle, actual.RoleTitle);
            Assert.Equal(expected.IsLocked, actual.IsLocked);
            Assert.Equal(expected.Username, actual.Username);
            Assert.Equal(expected.Email, actual.Email);
            Assert.Equal(expected.Id, actual.Id);
        }

        #endregion

        #region Method: GetViews()

        [Fact]
        public void GetViews_ReturnsAccountViews()
        {
            IEnumerator<AccountView> actual = service.GetViews().GetEnumerator();
            IEnumerator<AccountView> expected = context
                .Set<Account>()
                .ProjectTo<AccountView>()
                .OrderByDescending(view => view.CreationDate)
                .GetEnumerator();

            while (expected.MoveNext() | actual.MoveNext())
            {
                Assert.Equal(expected.Current.CreationDate, actual.Current.CreationDate);
                Assert.Equal(expected.Current.RoleTitle, actual.Current.RoleTitle);
                Assert.Equal(expected.Current.IsLocked, actual.Current.IsLocked);
                Assert.Equal(expected.Current.Username, actual.Current.Username);
                Assert.Equal(expected.Current.Email, actual.Current.Email);
                Assert.Equal(expected.Current.Id, actual.Current.Id);
            }
        }

        #endregion

        #region Method: IsLoggedIn(IPrincipal user)

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsLoggedIn_ReturnsIsAuthenticated(Boolean expected)
        {
            IPrincipal user = Substitute.For<IPrincipal>();
            user.Identity.IsAuthenticated.Returns(expected);

            Boolean actual = service.IsLoggedIn(user);

            Assert.Equal(expected, actual);
        }

        #endregion

        #region Method: IsActive(String id)

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsActive_ReturnsAccountState(Boolean isLocked)
        {
            account.IsLocked = isLocked;
            context.SaveChanges();

            Boolean actual = service.IsActive(account.Id);
            Boolean expected = !isLocked;

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void IsActive_NoAccount_ReturnsFalse()
        {
            Assert.False(service.IsActive("Test"));
        }

        #endregion

        #region Method: Recover(AccountRecoveryView view)

        [Fact]
        public void Recover_NoEmail_ReturnsNull()
        {
            AccountRecoveryView view = ObjectFactory.CreateAccountRecoveryView();
            view.Email = "not@existing.email";

            Assert.Null(service.Recover(view));
        }

        [Fact]
        public void Recover_Information()
        {
            account = context.Set<Account>().AsNoTracking().Single();
            account.RecoveryTokenExpirationDate = DateTime.Now.AddMinutes(30);

            AccountRecoveryView view = ObjectFactory.CreateAccountRecoveryView();
            view.Email = view.Email.ToUpper();

            String expectedToken = service.Recover(view);

            Account actual = context.Set<Account>().AsNoTracking().Single();
            Account expected = account;

            Assert.InRange(actual.RecoveryTokenExpirationDate.Value.Ticks,
                expected.RecoveryTokenExpirationDate.Value.Ticks - TimeSpan.TicksPerSecond,
                expected.RecoveryTokenExpirationDate.Value.Ticks + TimeSpan.TicksPerSecond);
            Assert.NotEqual(expected.RecoveryToken, actual.RecoveryToken);
            Assert.Equal(expected.CreationDate, actual.CreationDate);
            Assert.Equal(expectedToken, actual.RecoveryToken);
            Assert.Equal(expected.IsLocked, actual.IsLocked);
            Assert.Equal(expected.Passhash, actual.Passhash);
            Assert.Equal(expected.Username, actual.Username);
            Assert.Equal(expected.RoleId, actual.RoleId);
            Assert.Equal(expected.Email, actual.Email);
            Assert.Equal(expected.Id, actual.Id);
            Assert.NotNull(actual.RecoveryToken);
        }

        #endregion

        #region Method: Register(AccountRegisterView view)

        [Fact]
        public void Register_Account()
        {
            AccountRegisterView view = ObjectFactory.CreateAccountRegisterView(2);
            view.Email = view.Email.ToUpper();

            service.Register(view);

            Account actual = context.Set<Account>().AsNoTracking().Single(model => model.Id == view.Id);
            AccountRegisterView expected = view;

            Assert.Equal(hasher.HashPassword(expected.Password), actual.Passhash);
            Assert.Equal(expected.CreationDate, actual.CreationDate);
            Assert.Equal(expected.Email.ToLower(), actual.Email);
            Assert.Equal(expected.Username, actual.Username);
            Assert.Null(actual.RecoveryTokenExpirationDate);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Null(actual.RecoveryToken);
            Assert.False(actual.IsLocked);
            Assert.Null(actual.RoleId);
            Assert.Null(actual.Role);
        }

        #endregion

        #region Method: Reset(AccountResetView view)

        [Fact]
        public void Reset_Account()
        {
            AccountResetView view = ObjectFactory.CreateAccountResetView();
            account = context.Set<Account>().AsNoTracking().Single();
            account.Passhash = hasher.HashPassword(view.NewPassword);
            account.RecoveryTokenExpirationDate = null;
            account.RecoveryToken = null;

            service.Reset(view);

            Account actual = context.Set<Account>().AsNoTracking().Single();
            Account expected = account;

            Assert.Equal(expected.RecoveryTokenExpirationDate, actual.RecoveryTokenExpirationDate);
            Assert.Equal(expected.RecoveryToken, actual.RecoveryToken);
            Assert.Equal(expected.CreationDate, actual.CreationDate);
            Assert.Equal(expected.IsLocked, actual.IsLocked);
            Assert.Equal(expected.Passhash, actual.Passhash);
            Assert.Equal(expected.Username, actual.Username);
            Assert.Equal(expected.RoleId, actual.RoleId);
            Assert.Equal(expected.Email, actual.Email);
            Assert.Equal(expected.Id, actual.Id);
        }

        #endregion

        #region Method: Create(AccountCreateView view)

        [Fact]
        public void Create_Account()
        {
            AccountCreateView view = ObjectFactory.CreateAccountCreateView(2);
            view.Email = view.Email.ToUpper();
            view.RoleId = account.RoleId;

            service.Create(view);

            Account actual = context.Set<Account>().AsNoTracking().Single(model => model.Id == view.Id);
            AccountCreateView expected = view;

            Assert.Equal(hasher.HashPassword(expected.Password), actual.Passhash);
            Assert.Equal(expected.CreationDate, actual.CreationDate);
            Assert.Equal(expected.Email.ToLower(), actual.Email);
            Assert.Equal(expected.Username, actual.Username);
            Assert.Null(actual.RecoveryTokenExpirationDate);
            Assert.Equal(expected.RoleId, actual.RoleId);
            Assert.Equal(expected.Id, actual.Id);
            Assert.Null(actual.RecoveryToken);
            Assert.False(actual.IsLocked);
        }

        [Fact]
        public void Create_RefreshesAuthorization()
        {
            AccountCreateView view = ObjectFactory.CreateAccountCreateView(2);
            view.RoleId = null;

            service.Create(view);

            authorizationProvider.Received().Refresh();
        }

        #endregion

        #region Method: Edit(AccountEditView view)

        [Fact]
        public void Edit_Account()
        {
            AccountEditView view = ObjectFactory.CreateAccountEditView();
            account = context.Set<Account>().AsNoTracking().Single();
            view.IsLocked = account.IsLocked = !account.IsLocked;
            view.Username = account.Username + "Test";
            view.RoleId = account.RoleId = null;
            view.Email = account.Email + "s";

            service.Edit(view);

            Account actual = context.Set<Account>().AsNoTracking().Single();
            Account expected = account;

            Assert.Equal(expected.RecoveryTokenExpirationDate, actual.RecoveryTokenExpirationDate);
            Assert.Equal(expected.RecoveryToken, actual.RecoveryToken);
            Assert.Equal(expected.CreationDate, actual.CreationDate);
            Assert.Equal(expected.IsLocked, actual.IsLocked);
            Assert.Equal(expected.Username, actual.Username);
            Assert.Equal(expected.Passhash, actual.Passhash);
            Assert.Equal(expected.RoleId, actual.RoleId);
            Assert.Equal(expected.Email, actual.Email);
            Assert.Equal(expected.Id, actual.Id);
        }

        [Fact]
        public void Edit_RefreshesAuthorization()
        {
            AccountEditView view = ObjectFactory.CreateAccountEditView();

            service.Edit(view);

            authorizationProvider.Received().Refresh();
        }

        #endregion

        #region Method: Edit(ProfileEditView view)

        [Fact]
        public void Edit_Profile()
        {
            ProfileEditView view = ObjectFactory.CreateProfileEditView(2);
            account = context.Set<Account>().AsNoTracking().Single();
            account.Passhash = hasher.HashPassword(view.NewPassword);
            view.Username = account.Username += "Test";
            view.Email = account.Email += "Test";

            service.Edit(view);

            Account actual = context.Set<Account>().AsNoTracking().Single();
            Account expected = account;

            Assert.Equal(expected.RecoveryTokenExpirationDate, actual.RecoveryTokenExpirationDate);
            Assert.Equal(expected.RecoveryToken, actual.RecoveryToken);
            Assert.Equal(expected.CreationDate, actual.CreationDate);
            Assert.Equal(expected.Email.ToLower(), actual.Email);
            Assert.Equal(expected.IsLocked, actual.IsLocked);
            Assert.Equal(expected.Username, actual.Username);
            Assert.Equal(expected.Passhash, actual.Passhash);
            Assert.Equal(expected.RoleId, actual.RoleId);
            Assert.Equal(expected.Id, actual.Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("   ")]
        public void Edit_NullOrEmptyNewPassword_DoesNotEditPassword(String newPassword)
        {
            String passhash = context.Set<Account>().AsNoTracking().Single().Passhash;
            ProfileEditView view = ObjectFactory.CreateProfileEditView();
            view.NewPassword = newPassword;

            service.Edit(view);

            String actual = context.Set<Account>().AsNoTracking().Single().Passhash;
            String expected = passhash;

            Assert.Equal(expected, actual);
        }

        #endregion

        #region Method: Delete(String id)

        [Fact]
        public void Delete_Account()
        {
            service.Delete(account.Id);

            Assert.Empty(context.Set<Account>());
        }

        [Fact]
        public void Delete_RefreshesAuthorization()
        {
            service.Delete(account.Id);

            authorizationProvider.Received().Refresh();
        }

        #endregion

        #region Method: Login(AuthenticationManager authentication, String username)

        [Fact]
        public void Login_Account()
        {
            AuthenticationManager authentication = Substitute.For<AuthenticationManager>();

            service.Login(authentication, account.Username.ToUpper());

            authentication.Received().SignInAsync("Cookies", Arg.Is<ClaimsPrincipal>(principal =>
                principal.Claims.Single().Subject.NameClaimType == "name" &&
                principal.Claims.Single().Subject.RoleClaimType == "role" &&
                principal.Claims.Single().Subject.Name == account.Id &&
                principal.Identity.AuthenticationType == "local" &&
                principal.Identity.Name == account.Id &&
                principal.Identity.IsAuthenticated));
        }

        #endregion

        #region Method: Logout(AuthenticationManager authentication)

        [Fact]
        public void Logout_Account()
        {
            AuthenticationManager authentication = Substitute.For<AuthenticationManager>();

            service.Logout(authentication);

            authentication.Received().SignOutAsync("Cookies");
        }

        #endregion

        #region Test helpers

        private void SetUpData()
        {
            account = ObjectFactory.CreateAccount();

            context.Add(account.Role);
            context.Add(account);

            context.SaveChanges();
        }

        #endregion
    }
}