using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SchoolAdmission.Tests;

public sealed class AdmissionApiTests : IClassFixture<AdmissionFactory>
{
    private readonly HttpClient _client;
    public AdmissionApiTests(AdmissionFactory factory) => _client = factory.CreateClient();

    private async Task<string> CreateClass(int seats)
    {
        var response = await _client.PostAsJsonAsync("/classes", new { name = $"Test class {Guid.NewGuid():N}", totalSeats = seats });
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<HttpResponseMessage> Hold(string classId, string student)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{classId}/holds");
        request.Headers.Add("X-Student-Id", student);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task CreateClass_ReturnsCreatedClass()
    {
        var response = await _client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 12 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(12, json.RootElement.GetProperty("totalSeats").GetInt32());
    }

    [Fact]
    public async Task ConfirmingSameHoldTwice_IsIdempotent()
    {
        var classId = await CreateClass(2);
        const string student = "20152691234567890";
        using var created = await Hold(classId, student);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var holdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var holdId = holdJson.RootElement.GetProperty("holdId").GetString();
        for (var i = 0; i < 2; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/holds/{holdId}/confirm");
            request.Headers.Add("X-Student-Id", student);
            using var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var classes = JsonDocument.Parse(await _client.GetStringAsync("/classes"));
        var item = classes.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetString() == classId);
        Assert.Equal(1, item.GetProperty("confirmedSeats").GetInt32());
    }

    [Fact]
    public async Task ConcurrentApplications_NeverExceedSeatCapacity()
    {
        const int capacity = 5;
        var classId = await CreateClass(capacity);
        var requests = Enumerable.Range(0, 40).Select(i => Hold(classId, (20000000000000000L + i).ToString())).ToArray();
        var responses = await Task.WhenAll(requests);
        try
        {
            Assert.Equal(capacity, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
            Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
            using var classes = JsonDocument.Parse(await _client.GetStringAsync("/classes"));
            var item = classes.RootElement.EnumerateArray().Single(x => x.GetProperty("id").GetString() == classId);
            Assert.Equal(capacity, item.GetProperty("heldSeats").GetInt32() + item.GetProperty("confirmedSeats").GetInt32());
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }
}

public sealed class AdmissionFactory : WebApplicationFactory<Program>
{
    public AdmissionFactory()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_DATABASE_URL")))
            throw new InvalidOperationException("Set TEST_DATABASE_URL to a disposable PostgreSQL test database before running integration tests.");
        Environment.SetEnvironmentVariable("DATABASE_URL", Environment.GetEnvironmentVariable("TEST_DATABASE_URL"));
        Environment.SetEnvironmentVariable("HOLD_DURATION_SECONDS", "600");
    }
}
