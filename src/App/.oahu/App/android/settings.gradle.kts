pluginManagement {
    repositories { google(); mavenCentral(); gradlePluginPortal() }
}
dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
        maven {
            url = uri(file("../../../../../../oahu-feed/maven"))
            content { includeGroup("com.adamessenmacher.oahu") }
        }
    }
}
rootProject.name = "oahu-native"
include(":oahu-screens")
