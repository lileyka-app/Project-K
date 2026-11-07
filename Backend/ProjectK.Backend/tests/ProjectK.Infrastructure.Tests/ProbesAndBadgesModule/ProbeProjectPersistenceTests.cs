using Microsoft.EntityFrameworkCore;
using ProjectK.Common.Entities.ProbesAndBadgesModule;
using ProjectK.Common.Models.Records;
using ProjectK.Infrastructure.DbContexts;
using ProjectK.Infrastructure.Repositories.ProbesAndBadgesModule;
using ProjectK.Infrastructure.Services.BlobStorageService;
using Xunit;

namespace ProjectK.Infrastructure.Tests.ProbesAndBadgesModule;

/// <summary>
/// A project's pictures live only while a row names them: the orphan cleanup keeps what the rows
/// list and sweeps the rest of their folder, so a removed project or person frees its files.
/// </summary>
public class ProbeProjectPersistenceTests
{
    private static DbContextOptions<AppDbContext> Options() =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static ProbeProject Project(Guid memberKey, params string[] blobs)
    {
        var project = new ProbeProject { MemberKey = memberKey, ProbeId = "probe-3", Title = "Похід" };
        foreach (var blob in blobs)
        {
            project.Images.Add(new ProbeProjectImage { BlobName = blob });
        }
        return project;
    }

    [Fact]
    public void TheCleanupSweepsTheProjectFolder()
    {
        Assert.Contains(BlobUploadFolders.ProbeProjectImages, BlobUploadFolders.ScenarioFolders);
        Assert.StartsWith(BlobUploadFolders.ProbeProjectImages, BlobUploadContext.ProbeProjectImage.Folder);
        Assert.Contains(BlobUploadFolders.ThirdProbeCertificates, BlobUploadFolders.ScenarioFolders);
    }

    [Fact]
    public async Task ACampPosvidka_IsKeptByItsFile()
    {
        var options = Options();
        await using (var context = new AppDbContext(options))
        {
            context.ThirdProbeFiles.Add(new ThirdProbeFile
            {
                MemberKey = Guid.NewGuid(), ProbeId = "probe-3", CampCertificateBlobName = "third-probe-certificates/p.pdf"
            });
            context.ProbeProjects.Add(Project(Guid.NewGuid(), "probe-project-images/a.jpg"));
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            var referenced = await new ProbeProjectImageReferenceProvider(context).GetAllReferencedBlobNamesAsync(CancellationToken.None);
            Assert.Equal(["probe-project-images/a.jpg", "third-probe-certificates/p.pdf"], referenced.Order());
        }
    }

    [Theory]
    [InlineData("%PDF-1.7 rest", true)]
    [InlineData("<svg onload=alert(1)>", false)]
    [InlineData("%PDF", false)]
    public void APdf_IsToldByItsBytes(string content, bool expected)
    {
        Assert.Equal(expected, AzureBlobPhotoService.IsPdf(System.Text.Encoding.ASCII.GetBytes(content)));
    }

    [Fact]
    public async Task ThePictureRowsAreWhatTheCleanupKeeps_AndGoWithTheirProject()
    {
        var options = Options();
        var memberKey = Guid.NewGuid();
        var kept = Project(memberKey, "probe-project-images/a.jpg", "probe-project-images/b.jpg");
        var removed = Project(memberKey, "probe-project-images/c.jpg");

        await using (var context = new AppDbContext(options))
        {
            context.ProbeProjects.AddRange(kept, removed);
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            var repository = new ProbeProjectRepository(context);
            repository.Delete((await repository.GetWithImagesAsync(removed.ProbeProjectKey))!);
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            var referenced = await new ProbeProjectImageReferenceProvider(context).GetAllReferencedBlobNamesAsync(CancellationToken.None);
            Assert.Equal(["probe-project-images/a.jpg", "probe-project-images/b.jpg"], referenced.Order());
        }
    }

    [Fact]
    public async Task ARemovedPerson_TakesTheirProjectsAndPictureRowsAlong()
    {
        var options = Options();
        var gone = Guid.NewGuid();
        var stays = Guid.NewGuid();

        await using (var context = new AppDbContext(options))
        {
            context.ProbeProjects.AddRange(Project(gone, "probe-project-images/x.jpg"), Project(stays, "probe-project-images/y.jpg"));
            context.ThirdProbeFiles.Add(new ThirdProbeFile { MemberKey = gone, ProbeId = "probe-3", CampCertificateBlobName = "third-probe-certificates/z.pdf" });
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            await new ProbeProjectRepository(context).DeleteForMembersAsync([gone]);
            await context.SaveChangesAsync();
        }

        await using (var context = new AppDbContext(options))
        {
            Assert.Equal(["probe-project-images/y.jpg"], await new ProbeProjectRepository(context).GetAllImageBlobNamesAsync());
        }
    }
}
