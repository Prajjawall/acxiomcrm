using System.Reflection;
using AcxiomCrm.Domain.Security;
using AcxiomCrm.Web.Controllers;
using AcxiomCrm.Web.ViewModels.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace AcxiomCrm.Web.Tests;

public class UserAdminAccessTests
{
    [Fact]
    public void UsersController_has_expected_authorize_and_antiforgery_attributes()
    {
        var controllerType = typeof(UsersController);

        var controllerAuth = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        controllerAuth.ShouldNotBeNull();
        controllerAuth.Policy.ShouldBe(Permissions.UsersView);

        // Create
        var createGet = controllerType.GetMethod(nameof(UsersController.Create), [typeof(CancellationToken)]);
        createGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.UsersManage);

        var createPost = controllerType.GetMethod(nameof(UsersController.Create), [typeof(UserFormViewModel), typeof(CancellationToken)]);
        createPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.UsersManage);
        createPost?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().ShouldNotBeNull();

        // Edit
        var editGet = controllerType.GetMethod(nameof(UsersController.Edit), [typeof(string), typeof(CancellationToken)]);
        editGet?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.UsersManage);

        var editPost = controllerType.GetMethod(nameof(UsersController.Edit), [typeof(string), typeof(UserFormViewModel), typeof(CancellationToken)]);
        editPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.UsersManage);
        editPost?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().ShouldNotBeNull();

        // Activate
        var activatePost = controllerType.GetMethod(nameof(UsersController.Activate), [typeof(string), typeof(CancellationToken)]);
        activatePost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.UsersManage);
        activatePost?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().ShouldNotBeNull();

        // Deactivate
        var deactivatePost = controllerType.GetMethod(nameof(UsersController.Deactivate), [typeof(string), typeof(string), typeof(CancellationToken)]);
        deactivatePost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.UsersManage);
        deactivatePost?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().ShouldNotBeNull();

        // Unlock
        var unlockPost = controllerType.GetMethod(nameof(UsersController.Unlock), [typeof(string), typeof(CancellationToken)]);
        unlockPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.UsersUnlock);
        unlockPost?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().ShouldNotBeNull();

        // ResetPassword
        var resetPost = controllerType.GetMethod(nameof(UsersController.ResetPassword), [typeof(string), typeof(CancellationToken)]);
        resetPost?.GetCustomAttribute<AuthorizeAttribute>()?.Policy.ShouldBe(Permissions.UsersResetPassword);
        resetPost?.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>().ShouldNotBeNull();
    }

    [Fact]
    public void RolesController_has_RolesView_authorize_attribute()
    {
        var controllerType = typeof(RolesController);
        var controllerAuth = controllerType.GetCustomAttribute<AuthorizeAttribute>();
        controllerAuth.ShouldNotBeNull();
        controllerAuth.Policy.ShouldBe(Permissions.RolesView);
    }
}
