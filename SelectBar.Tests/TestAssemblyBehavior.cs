// L.Culture est un état statique partagé : les tests s'exécutent un par un pour que la langue de l'un ne change pas celle de l'autre.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
