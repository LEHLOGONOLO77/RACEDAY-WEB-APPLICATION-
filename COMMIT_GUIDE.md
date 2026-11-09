# Suggested commit sequence

Make each commit when that step works. Do not squash them. Keep Part 3 on its own branch (for example `part-3`) and open a pull request, or commit to the `RaceDay.Web` folder. Delete this file before submitting if you like.

## Part 3 (aim for 20+ real commits)

1. Create `part-3` branch and add `RaceDay.Web` MVC project to the solution
2. Add `appsettings` API base URL and fixed `en-US` culture
3. Add API response models and form view models
4. Add `ApiClient` (GET / POST / PUT / DELETE with JSON)
5. Add session setup and storing the API cookie in the session
6. Add friendly API error handling
7. Add `SessionAuth` filter and base controller
8. Add `AccountController` (login, register, logout) and views
9. Add shared layout with role-based navigation
10. Add RaceDay theme (`site.css`) and status colour partial
11. Add Home page with event cards and filters
12. Add Event details page with categories
13. Add enrol flow and My enrolments (pay, cancel)
14. Add My results page with positions
15. Add Profile page
16. API: add `BannerUrl` / `ProfilePictureUrl` and migration `AddImageUrls`
17. API: add Azure Blob Storage service and upload endpoints
18. API: add upload tests with a fake blob store
19. Add Organiser dashboard
20. Add Organiser events management with banner upload
21. Add Organiser categories page
22. Add Organiser enrolments page
23. Add Organiser results entry page
24. Add friendly error and 404 pages
25. Add Dockerfile and `.dockerignore`
26. Update GitHub Actions to build the MVC project and Docker image
27. Update README with Docker run command and Azure setup
28. Add YouTube link and green build screenshot

## Part 2 (history)

1. Create solution and API project (`dotnet new`)
2. Add NuGet packages and `.gitignore`
3. Add enums and `User`/`Organiser`/`Participant` entities
... (see Part 2 submission)
