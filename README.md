**NOTE:** I have added github as an origin but for the latest commits and issue tracking please visit https://git.hobbs.zone/henry/AccessQueueService

# AccessQueueService

AccessQueueService is a microservice API designed to control access to a resource with a limited number of concurrent users. It ensures fair access by:

- Granting immediate access if capacity is available.
- Placing users in a queue when the resource is full.
- Automatically managing the queue in a first-in, first-out (FIFO) order.
- Allowing users to revoke their access, freeing up capacity for others.

This service is ideal for scenarios where you need to limit the number of users accessing a resource at the same time, such as online ticket sale platforms that control how many users can purchase tickets concurrently.

Note: This service is not intended to be called directly from end-user client applications, as it could be easily bypassed. Instead, it should be integrated as middleware within your own APIs or backend services.

## How the Service Works

1. **Requesting Access:**
   - When a user requests access, the service checks if the current number of active users is below `CapacityLimit`.
   - If there is capacity, the user is granted access immediately and receives an expiration date set by `ExpirationSeconds`.
   - If not, the user is added to a queue and receives their position in the queue.

2. **Queueing:**
   - If a user is placed in the queue, subsequent access requests will return the number of users ahead.
   - Users must continually re-request access to remain active in the queue; inactivity may result in losing their spot.

3. **Dequeuing:**
   - Users in the queue are managed in a FIFO (first-in, first-out) order.
   - Whenever an access request is made, if there is capacity, the service attempts to dequeue users until capacity is met.
   - If a user is dequeued but the time since their last activity is greater than `ActivitySeconds`, they are not granted access and lose their spot in the queue.

4. **Maintaining Access:**
   - Users should continually re-request access while they are active to avoid being considered inactive.
   - If `RollingExpiration` is enabled, the expiration is reset whenever access is re-requested.

5. **Revoking Access:**
   - If a user requests access after their expiration date, they must restart the process and re-join the queue if there isn't capacity.
   - When a user revokes access (or their access times out), their access expires immediately.
   
### Note on inactivity vs expiration

It is possible for the number of users with access to temporarily exceed the `CapacityLimit` if `ActivitySeconds` is less than `ExpirationSeconds`. This happens because:

- The number of available slots is determined by the time since a user's last activity (`ActivitySeconds`), not by their access expiration (`ExpirationSeconds`).
- If a user is inactive for longer than `ActivitySeconds`, they no longer count toward the capacity, allowing another user to gain access.
- However, the original user still technically has access until their `ExpirationSeconds` elapses.

**To ensure the number of users with access never exceeds `CapacityLimit`, set `ActivitySeconds` equal to `ExpirationSeconds`.**

## API Routes

### Request Access
- **GET /access/{id}**
  - **Description:** Request access for a user with the specified `id`.
  - **Response:** Returns an `AccessResponse` object indicating whether access was granted or the user's position in the queue.

### Revoke Access
- **DELETE /access/{id}**
  - **Description:** Revoke access for a user with the specified `id`. This will remove the user from the active list or queue and may allow the next user in the queue to gain access.
  - **Response:** Returns a boolean indicating success.

## Configuration Variables

Configuration is set in `appsettings.json` or via environment variables. The main configuration section is `AccessQueue`:

- **CapacityLimit**: The maximum number of users that can have access at the same time (default: 100).
- **ActivitySeconds**: How long (in seconds) a user can remain active before being considered inactive (default: 900).
- **ExpirationSeconds**: How long (in seconds) before an access ticket expires (default: 43200).
- **RollingExpiration**: If true, the expiration timer resets on activity (default: true).
- **CleanupIntervalSeconds**: How often (in seconds) the background cleanup runs to remove expired/inactive users (default: 60).

Example `appsettings.json`:
```json
{
  "AccessQueue": {
    "CapacityLimit": 100,
    "ActivitySeconds": 900,
    "ExpirationSeconds": 43200,
    "RollingExpiration": true,
    "CleanupIntervalSeconds": 60
  }
}
```

## AccessResponse Object

The `AccessResponse` object returned by the API contains the following properties:

- **ExpiresOn** (`DateTime?`): The UTC timestamp when the user's access will expire. `null` if the user does not have access.
- **RequestsAhead** (`int`): The number of requests ahead of the user in the queue. `0` if the user has access.
- **HasAccess** (`bool`): Indicates whether the user currently has access (true if `ExpiresOn` is set and in the future).

## Running the Service

1. Build and run the project using .NET 8.0 or later:
   ```powershell
   dotnet run --project AccessQueueService/AccessQueueService.csproj
   ```
2. By default, the API will be available at:
   - HTTP: http://localhost:5199
   - HTTPS: https://localhost:7291
   (See `AccessQueueService/Properties/launchSettings.json` for details.)

## Running the Tests

Unit tests for the service are located in the `AccessQueueServiceTests` project. To run all tests, use the following command from the root of the repository:

```powershell
# Run all tests in the solution
 dotnet test
```

Test results will be displayed in the terminal. You can also use Visual Studio's Test Explorer for a graphical interface.

## AccessQueuePlayground (Demo UI)

The `AccessQueuePlayground` project provides a simple web-based UI for interacting with the AccessQueueService API. This is useful for testing and demonstration purposes.

### Running the Playground

1. Build and run the playground project:
   ```powershell
   dotnet run --project AccessQueuePlayground/AccessQueuePlayground.csproj
   ```
2. By default, the playground will be available at:
   - HTTP: http://localhost:5108
   - HTTPS: https://localhost:7211
   (See `AccessQueuePlayground/Properties/launchSettings.json` for details.)

### Using the Playground

- Open the provided URL in your browser.
- Use the UI to request and revoke access for different user IDs.
- The UI will display your access status, queue position, and expiration time.

This playground is intended only for local development and demonstration.

## License
See [LICENSE.txt](./LICENSE.txt) for license information.