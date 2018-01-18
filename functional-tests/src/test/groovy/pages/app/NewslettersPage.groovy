	package pages.app

import geb.spock.GebReportingSpec
import java.util.List;
import java.util.Iterator

import org.openqa.selenium.*

import pages.app.HomePage
import pages.app.NewslettersPage

import spock.lang.Specification

import geb.Page

class NewslettersPage extends Page {

    static at = { title=="Newsletters | BC Gov News" }
    static url = "/newsletters"


	boolean areHeadersSorted()
	{
		WebElement top = driver.findElement(By.cssSelector("div[class='home-body']"))
		
		List<WebElement> sectionHeaders = top.findElements(By.xpath("//div/h4"))
		println("*****************")
		println(sectionHeaders.size())
		println("*****************")

		for (int i=0; i<sectionHeaders.size()-2;i++)
		{
			if (sectionHeaders.get(i).Text().campareTo(sectionHeaders.get(i+1).Text())>0)
			{
				return false
			}
		}
		return true
	}
}
