# ETL system til allokering af indkommende pakker til pakkeskabe

## Forventning til opgavebesvarelsen

Opgaven er løst defineret, og det er med vilje ikke muligt at nå det hele. Se målet med besvarelsen som en kombination af at:

* vise robust kode 
* skabe et samtalegrundlag for mulige forbedringer 

Til vores næste samtale har vi afsat ca. 30 min til at tale om opgaven.

Vi forventer, at du højst bruger 8 timer på opgaven.

## Baggrund og problemformulering

Et fiktivt, dansk pakkeuddelingsselskab udvider deres forretning og kundeservice ved at sætte pakke-bokse op i forskellige byer. Folk er allerede begyndt at bestille leveringer til dem, og der mangler et system, der kan reservere bokse og give besked til modtagerne.

Vores fiktive selskab har et batchorienteret kernesystem, så information om pakker, modtagere osv. leveres ca. en gang i døgnet som CSV. Opgaven er at læse denne data, allokere boks og genere pinkoder som kan sendes til modtageren.

* **datasource.csv**  indeholder vores kernesystemsgenerede batch.

Der medfølger API-specifikationer på to endpoints:

* [`locker-api.yaml`](locker-api.yaml)
  * håndterer pakkebokse og skabe i disse
* [`notification-api.yaml`](notification-api.yaml)
  * håndterer beskeder til kunderne

Når der er allokeret en boks og afsendt en besked, anses en pakke for 'leveret'. Vi tager ikke højde for:
- at sms'en ikke kom frem
- at chaufføren ikke kunne sætte pakken i boksen, fordi den var gået i stykker
- etc.

Som hjælp er der lavet et docker image der mocker de specificerede endpoints:

* Hent og start med Docker: `docker compose pull` og derefter `docker compose up --wait`
* Standard base-URL: `http://localhost:18080/api/v1`
* Interaktiv Swagger / OpenAPI dokumentation: `http://localhost:18080/swagger`

## 5. Aflevering

Brug gerne det programmeringssprog og framework, du føler dig mest tryg og stærk i. Du skal aflevere din løsning i et offentligt GitHub repo. Den skal indeholde din kildekode, tests samt en kortfattet `README`, der beskriver, hvordan din løsning afvikles.
